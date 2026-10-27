using CodeWalker.GameFiles;
using GTARadioEditor.Models;
using System.Security.Cryptography;

namespace GTARadioEditor.Services;

public sealed class RpfRadioService
{
    public async Task<IReadOnlyList<RadioSlot>> ScanMusicSlotsAsync(
        string rpfPath,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rpf = OpenRpf(rpfPath, progress);
            var slots = new List<RadioSlot>();

            foreach (var entry in rpf.AllEntries.OfType<RpfFileEntry>()
                         .Where(entry => entry.NameLower.EndsWith(".awc", StringComparison.OrdinalIgnoreCase))
                         .OrderBy(entry => entry.NameLower, StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(L.T("Inspecting {0}...", entry.Name));
                var awc = LoadAwc(entry);
                if (awc.ErrorMessage is not null)
                {
                    continue;
                }

                var pair = FindStereoPair(awc);
                if (pair is null)
                {
                    continue;
                }

                var (left, right) = pair.Value;
                var duration = TimeSpan.FromSeconds(Math.Max(left.Length, right.Length));
                // Station IDs are also small stereo AWC files. Treat short clips as imaging,
                // not music slots, so a radio with 29 songs reports 29 assignable rows.
                if (duration < TimeSpan.FromSeconds(30))
                {
                    continue;
                }
                slots.Add(new RadioSlot
                {
                    ContainerName = entry.Name,
                    ContainerPath = entry.Path,
                    LeftChannelName = left.Name,
                    RightChannelName = right.Name,
                    SampleRate = left.SamplesPerSecond,
                    OriginalDuration = duration
                });
            }

            progress?.Report(L.T("Found {0} replaceable music containers.", slots.Count));
            return (IReadOnlyList<RadioSlot>)slots;
        }, cancellationToken);
    }

    public async Task<BuildResult> BuildOutputAsync(
        string sourceRpfPath,
        string outputRpfPath,
        IEnumerable<RadioSlot> slots,
        string sourceTimingPath,
        IProgress<string>? progress = null,
        IProgress<int>? buildProgress = null,
        CancellationToken cancellationToken = default)
    {
        var selected = slots.Where(slot => !string.IsNullOrWhiteSpace(slot.ReplacementPath)).ToList();
        if (selected.Count == 0)
        {
            throw new InvalidOperationException(L.T("Assign at least one replacement track before building an output RPF."));
        }
        if (Path.GetFullPath(sourceRpfPath).Equals(Path.GetFullPath(outputRpfPath), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(L.T("The output RPF must be a new file. The selected source RPF is never edited."));
        }
        if (!Path.GetFileName(sourceRpfPath).Equals(Path.GetFileName(outputRpfPath), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(L.T("The output RPF must keep the source archive's file name. Choose a new folder, not a renamed archive, so encrypted RPF headers remain valid."));
        }

        sourceRpfPath = Path.GetFullPath(sourceRpfPath);
        sourceTimingPath = Path.GetFullPath(sourceTimingPath);
        outputRpfPath = Path.GetFullPath(outputRpfPath);
        var outputTimingPath = Path.Combine(Path.GetDirectoryName(outputRpfPath)!, Path.GetFileName(sourceTimingPath));
        var sources = new[] { sourceRpfPath, sourceTimingPath }.Concat(selected.Select(s => Path.GetFullPath(s.ReplacementPath!))).ToList();
        if (new[] { outputRpfPath, outputTimingPath }.Any(output => sources.Contains(output, StringComparer.OrdinalIgnoreCase)))
            throw new InvalidOperationException(L.T("Choose a new output folder. Outputs must not overwrite source archives, timing metadata or replacement audio."));
        var sharedArchive = sourceRpfPath.Equals(sourceTimingPath, StringComparison.OrdinalIgnoreCase);
        if (!sharedArchive && outputTimingPath.Equals(outputRpfPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(L.T("The radio and timing sources have conflicting output file names."));

        buildProgress?.Report(0);
        return await Task.Run(async () =>
        {
            progress?.Report(L.T("Matching radio songs to streaming durations..."));
            var timing = RadioTimingService.Open(sourceTimingPath, sourceRpfPath, selected, progress, cancellationToken);
            var outputDirectory = Path.GetDirectoryName(outputRpfPath)
                ?? throw new InvalidOperationException(L.T("The output path does not include a directory."));
            Directory.CreateDirectory(outputDirectory);
            var stagingDirectory = Path.Combine(outputDirectory, ".gta-radio-build-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stagingDirectory);
            var stagedRadioPath = Path.Combine(stagingDirectory, Path.GetFileName(outputRpfPath));
            var stagedTimingPath = Path.Combine(stagingDirectory, Path.GetFileName(sourceTimingPath));
            var published = false;
            try
            {
                File.Copy(sourceRpfPath, stagedRadioPath);
                if (!sharedArchive) File.Copy(sourceTimingPath, stagedTimingPath);
                buildProgress?.Report(5);
                var rpf = OpenRpf(stagedRadioPath, progress);
                ConvertOutputToOpenRpf(rpf, progress);
                var notes = new List<string>();
                var expectedAudio = new Dictionary<string, (int Samples, byte[] Hash)>(StringComparer.OrdinalIgnoreCase);
                var completed = 0;
                buildProgress?.Report(10);

                foreach (var slot in selected)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var source = slot.ReplacementPath!;
                    buildProgress?.Report(10 + completed * 85 / selected.Count);
                    progress?.Report(L.T("Converting {0} ({1}/{2})...", Path.GetFileName(source), completed + 1, selected.Count));
                    var converted = await AudioConversionService.ConvertToGtaStereoPairAsync(source, cancellationToken);

                    var entry = rpf.AllEntries.OfType<RpfFileEntry>()
                        .SingleOrDefault(item => item.Path.Equals(slot.ContainerPath, StringComparison.OrdinalIgnoreCase));
                    if (entry is null)
                    {
                        throw new InvalidDataException(L.T("{0} is no longer present in the output RPF.", slot.ContainerName));
                    }

                    var awc = LoadAwc(entry, sourceRpfPath);
                    var pair = FindStereoPair(awc)
                        ?? throw new InvalidDataException(L.T("{0} no longer has a supported left/right channel pair.", slot.ContainerName));

                    progress?.Report(L.T("Rebuilding {0}...", slot.ContainerName));
                    if (pair.Left.SampleCount != pair.Right.SampleCount || pair.Left.SamplesPerSecond != pair.Right.SamplesPerSecond)
                        throw new InvalidDataException(L.T("{0} has unequal stereo channel lengths or sample rates.", slot.ContainerName));
                    RadioCueService.Retime(awc, pair.Left.SampleCount, converted.SampleCount);
                    timing.SetDuration(slot, converted.SampleCount);
                    ReplaceChannel(pair.Left, converted.LeftWave, converted.SampleCount);
                    ReplaceChannel(pair.Right, converted.RightWave, converted.SampleCount);
                    if (awc.MultiChannelFlag && awc.MultiChannelSource is not null)
                    {
                        awc.MultiChannelSource.CompactMultiChannelSources(awc.Streams);
                    }

                    // Data chunks change size whenever a user replaces a song. Rebuild the AWC's
                    // internal offsets before serializing so the resulting container can be parsed again.
                    awc.BuildPeakChunks();
                    awc.BuildChunkIndices();
                    awc.BuildStreamInfos();

                    var rebuiltAwc = awc.Save();
                    ValidateRebuiltAwc(rebuiltAwc, entry.Name, converted.SampleCount);
                    RpfFile.CreateFile(entry.Parent, entry.Name, rebuiltAwc);
                    using (var sha = SHA256.Create())
                        expectedAudio.Add(slot.ContainerPath, (converted.SampleCount, sha.ComputeHash(rebuiltAwc)));

                    completed++;
                    buildProgress?.Report(10 + completed * 85 / selected.Count);
                    notes.Add($"{slot.ContainerName} <- {Path.GetFileName(source)} ({converted.Duration:mm\\:ss})");
                }

                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(L.T("Writing and validating radio timing metadata..."));
                timing.Write(stagedTimingPath, progress);
                var finalRadio = OpenRpf(stagedRadioPath, progress);
                foreach (var slot in selected)
                {
                    var finalEntry = finalRadio.AllEntries.OfType<RpfFileEntry>().Single(e => e.Path.Equals(slot.ContainerPath, StringComparison.OrdinalIgnoreCase));
                    var data = finalEntry.File.ExtractFile(finalEntry);
                    var expected = expectedAudio[slot.ContainerPath];
                    ValidateRebuiltAwc(data, finalEntry.Name, expected.Samples);
                    using var sha = SHA256.Create();
                    if (!sha.ComputeHash(data).SequenceEqual(expected.Hash))
                        throw new InvalidDataException(L.T("The packaged {0} differs from the validated audio.", finalEntry.Name));
                }
                cancellationToken.ThrowIfCancellationRequested();
                var outputs = new List<(string Staged, string Output)> { (stagedRadioPath, outputRpfPath) };
                if (!sharedArchive) outputs.Add((stagedTimingPath, outputTimingPath));
                CommitOutputs(outputs, stagingDirectory);
                published = true;
                notes.Add(L.T("Timing metadata: {0}. Install it into the matching original game archive; both outputs are required.", outputTimingPath));
                progress?.Report(L.T("Built {0} with {1} replacement(s).", Path.GetFileName(outputRpfPath), completed));
                buildProgress?.Report(100);
                return new BuildResult(outputRpfPath, completed, notes, sourceTimingPath, outputTimingPath);
            }
            finally
            {
                if (published || !Directory.EnumerateFiles(stagingDirectory, "*.backup").Any())
                {
                    try { Directory.Delete(stagingDirectory, true); }
                    catch (IOException) { progress?.Report(L.T("Temporary build files could not be removed: {0}", stagingDirectory)); }
                    catch (UnauthorizedAccessException) { progress?.Report(L.T("Temporary build files could not be removed: {0}", stagingDirectory)); }
                }
            }
        }, cancellationToken);
    }

    internal static RpfFile OpenRpf(string rpfPath, IProgress<string>? progress)
    {
        if (!File.Exists(rpfPath))
        {
            throw new FileNotFoundException(L.T("The selected RPF file does not exist."), rpfPath);
        }

        using (var reader = new BinaryReader(File.OpenRead(rpfPath)))
        {
            if (reader.BaseStream.Length < 16) throw new InvalidDataException(L.T("The selected RPF header is incomplete."));
            reader.BaseStream.Position = 12;
            var encryption = (RpfEncryption)reader.ReadUInt32();
            if (encryption == RpfEncryption.AES || encryption == RpfEncryption.NG)
                EnsureKeysAreAvailable(rpfPath, progress);
        }
        var errors = new List<string>();
        var rpf = new RpfFile(rpfPath, Path.GetFileName(rpfPath));
        rpf.ScanStructure(progress is null ? null : message => progress.Report(
            message.StartsWith("Scanning ", StringComparison.Ordinal) ? L.T("Scanning {0}...", rpf.Path) : message), errors.Add);
        if (rpf.LastException is not null)
        {
            throw new InvalidDataException(L.T("Could not read {0}: {1}", Path.GetFileName(rpfPath), rpf.LastError), rpf.LastException);
        }
        if (errors.Count > 0)
        {
            throw new InvalidDataException(L.T("Could not scan {0}: {1}", Path.GetFileName(rpfPath), errors[0]));
        }
        return rpf;
    }

    private static void EnsureKeysAreAvailable(string rpfPath, IProgress<string>? progress, bool needsAwcKey = false)
    {
        if (needsAwcKey ? GTA5Keys.PC_AWC_KEY is not null : GTA5Keys.PC_AES_KEY is not null)
        {
            return;
        }

        var gameRoot = FindGameRoot(rpfPath);
        if (gameRoot is null)
        {
            throw new InvalidOperationException(
                L.T("Could not locate GTA5.exe above the selected RPF. Select an RPF inside a GTA V Legacy installation or its mods folder."));
        }

        progress?.Report(L.T("Loading encryption keys from the selected GTA V installation..."));
        GTA5Keys.LoadFromPath(gameRoot);
        if (needsAwcKey && GTA5Keys.PC_AWC_KEY is null)
            throw new InvalidOperationException(L.T("Could not load the audio encryption key from this GTA V installation."));
    }

    internal static void ConvertOutputToOpenRpf(RpfFile rpf, IProgress<string>? progress)
    {
        progress?.Report(L.T("Preparing the output RPF for mod use..."));
        RpfFile.EnsureValidEncryption(rpf, _ => true, recursive: true);
    }

    private static string? FindGameRoot(string rpfPath)
    {
        for (var current = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(rpfPath))!);
             current is not null;
             current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "GTA5.exe")))
            {
                return current.FullName;
            }
        }
        return null;
    }

    private static AwcFile LoadAwc(RpfFileEntry entry, string? keySourcePath = null)
    {
        var bytes = entry.File.ExtractFile(entry)
            ?? throw new InvalidDataException(L.T("Could not extract {0} from the RPF.", entry.Name));
        if (bytes.Length >= 8 && GTA5Keys.PC_AWC_KEY is null)
        {
            var magic = BitConverter.ToUInt32(bytes, 0);
            var encrypted = (magic != 0x54414441 && magic != 0x41444154) ||
                ((bytes[magic == 0x41444154 ? 7 : 6] & 10) != 0);
            if (encrypted) EnsureKeysAreAvailable(keySourcePath ?? entry.File.FilePath, null, needsAwcKey: true);
        }
        var awc = new AwcFile();
        awc.Load(bytes, entry);
        if (awc.ErrorMessage is not null)
        {
            throw new InvalidDataException(L.T("Could not parse {0}: {1}", entry.Name, awc.ErrorMessage));
        }
        return awc;
    }

    private static (AwcStream Left, AwcStream Right)? FindStereoPair(AwcFile awc)
    {
        var streams = awc.Streams ?? [];
        var left = streams.SingleOrDefault(stream => stream.Name.EndsWith("_LEFT", StringComparison.OrdinalIgnoreCase));
        var right = streams.SingleOrDefault(stream => stream.Name.EndsWith("_RIGHT", StringComparison.OrdinalIgnoreCase));
        return left is not null && right is not null ? (left, right) : null;
    }

    private static void ReplaceChannel(AwcStream stream, byte[] wave, int sampleCount)
    {
        stream.ParseWavFile(wave);
        if (stream.FormatChunk is not null)
        {
            stream.FormatChunk.Samples = (uint)sampleCount;
            stream.FormatChunk.SamplesPerSecond = AudioConversionService.GtaSampleRate;
        }
        if (stream.StreamFormat is not null)
        {
            stream.StreamFormat.Samples = (uint)sampleCount;
            stream.StreamFormat.SamplesPerSecond = AudioConversionService.GtaSampleRate;
        }
    }

    private static void ValidateRebuiltAwc(byte[] data, string awcName, int expectedSampleCount)
    {
        var validationEntry = new RpfBinaryFileEntry { Name = awcName, NameLower = awcName.ToLowerInvariant() };
        var validation = new AwcFile();
        validation.Load(data, validationEntry);
        if (validation.ErrorMessage is not null)
        {
            throw new InvalidDataException(L.T("The rebuilt {0} failed validation: {1}", awcName, validation.ErrorMessage));
        }
        var pair = FindStereoPair(validation)
            ?? throw new InvalidDataException(L.T("The rebuilt {0} no longer has a left/right channel pair.", awcName));
        if (pair.Left.SampleCount != expectedSampleCount || pair.Right.SampleCount != expectedSampleCount)
        {
            throw new InvalidDataException(L.T("The rebuilt {0} has an unexpected sample count.", awcName));
        }
        if (pair.Left.SamplesPerSecond != AudioConversionService.GtaSampleRate ||
            pair.Right.SamplesPerSecond != AudioConversionService.GtaSampleRate ||
            validation.Streams.Any(s => (s.MarkersChunk?.Markers ?? []).Any(m => m.SampleOffset > expectedSampleCount)))
            throw new InvalidDataException(L.T("The rebuilt {0} has invalid audio timing or cues.", awcName));
    }

    private static void CommitOutputs(IReadOnlyList<(string Staged, string Output)> files, string stagingDirectory)
    {
        var committed = new List<(string Output, string? Backup)>();
        try
        {
            foreach (var file in files)
            {
                string? backup = null;
                if (File.Exists(file.Output))
                {
                    backup = Path.Combine(stagingDirectory, Guid.NewGuid().ToString("N") + ".backup");
                    File.Replace(file.Staged, file.Output, backup);
                }
                else File.Move(file.Staged, file.Output);
                committed.Add((file.Output, backup));
            }
        }
        catch (Exception commitError)
        {
            try
            {
                foreach (var file in committed.AsEnumerable().Reverse())
                {
                    if (file.Backup is not null) File.Replace(file.Backup, file.Output, null);
                    else File.Delete(file.Output);
                }
            }
            catch (Exception rollbackError)
            {
                throw new IOException(L.T("Could not finish the output files or restore earlier outputs. Recovery files are retained in {0}.", stagingDirectory),
                    new AggregateException(commitError, rollbackError));
            }
            throw;
        }
    }
}
