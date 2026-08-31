using CodeWalker.GameFiles;
using GTARadioEditor.Models;

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
                progress?.Report($"Inspecting {entry.Name}...");
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

            progress?.Report($"Found {slots.Count} replaceable music containers.");
            return (IReadOnlyList<RadioSlot>)slots;
        }, cancellationToken);
    }

    public async Task<BuildResult> BuildOutputAsync(
        string sourceRpfPath,
        string outputRpfPath,
        IEnumerable<RadioSlot> slots,
        IProgress<string>? progress = null,
        IProgress<int>? buildProgress = null,
        CancellationToken cancellationToken = default)
    {
        var selected = slots.Where(slot => !string.IsNullOrWhiteSpace(slot.ReplacementPath)).ToList();
        if (selected.Count == 0)
        {
            throw new InvalidOperationException("Assign at least one replacement track before building an output RPF.");
        }
        if (Path.GetFullPath(sourceRpfPath).Equals(Path.GetFullPath(outputRpfPath), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The output RPF must be a new file. The selected source RPF is never edited.");
        }
        if (!Path.GetFileName(sourceRpfPath).Equals(Path.GetFileName(outputRpfPath), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The output RPF must keep the source archive's file name. Choose a new folder, not a renamed archive, so encrypted RPF headers remain valid.");
        }

        buildProgress?.Report(0);
        return await Task.Run(async () =>
        {
            var outputDirectory = Path.GetDirectoryName(outputRpfPath)
                ?? throw new InvalidOperationException("The output path does not include a directory.");
            Directory.CreateDirectory(outputDirectory);
            File.Copy(sourceRpfPath, outputRpfPath, true);
            buildProgress?.Report(5);

            try
            {
                var rpf = OpenRpf(outputRpfPath, progress);
                var notes = new List<string>();
                var completed = 0;
                buildProgress?.Report(10);

                foreach (var slot in selected)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var source = slot.ReplacementPath!;
                    buildProgress?.Report(10 + completed * 85 / selected.Count);
                    progress?.Report($"Converting {Path.GetFileName(source)} ({completed + 1}/{selected.Count})...");
                    var converted = await AudioConversionService.ConvertToGtaStereoPairAsync(source, cancellationToken);

                    var entry = rpf.AllEntries.OfType<RpfFileEntry>()
                        .SingleOrDefault(item => item.Name.Equals(slot.ContainerName, StringComparison.OrdinalIgnoreCase));
                    if (entry is null)
                    {
                        throw new InvalidDataException($"{slot.ContainerName} is no longer present in the output RPF.");
                    }

                    var awc = LoadAwc(entry);
                    var pair = FindStereoPair(awc)
                        ?? throw new InvalidDataException($"{slot.ContainerName} no longer has a supported left/right channel pair.");

                    progress?.Report($"Rebuilding {slot.ContainerName}...");
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

                    completed++;
                    buildProgress?.Report(10 + completed * 85 / selected.Count);
                    notes.Add($"{slot.ContainerName} <- {Path.GetFileName(source)} ({converted.Duration:mm\\:ss})");
                }

                progress?.Report($"Built {Path.GetFileName(outputRpfPath)} with {completed} replacement(s).");
                buildProgress?.Report(100);
                return new BuildResult(outputRpfPath, completed, notes);
            }
            catch
            {
                if (File.Exists(outputRpfPath))
                {
                    File.Delete(outputRpfPath);
                }
                throw;
            }
        }, cancellationToken);
    }

    private static RpfFile OpenRpf(string rpfPath, IProgress<string>? progress)
    {
        if (!File.Exists(rpfPath))
        {
            throw new FileNotFoundException("The selected RPF file does not exist.", rpfPath);
        }

        EnsureKeysAreAvailable(rpfPath, progress);
        var errors = new List<string>();
        var rpf = new RpfFile(rpfPath, Path.GetFileName(rpfPath));
        rpf.ScanStructure(progress is null ? null : progress.Report, errors.Add);
        if (rpf.LastException is not null)
        {
            throw new InvalidDataException($"Could not read {Path.GetFileName(rpfPath)}: {rpf.LastError}", rpf.LastException);
        }
        if (errors.Count > 0)
        {
            throw new InvalidDataException($"Could not scan {Path.GetFileName(rpfPath)}: {errors[0]}");
        }
        return rpf;
    }

    private static void EnsureKeysAreAvailable(string rpfPath, IProgress<string>? progress)
    {
        if (GTA5Keys.PC_AES_KEY is not null)
        {
            return;
        }

        var gameRoot = FindGameRoot(rpfPath);
        if (gameRoot is null)
        {
            throw new InvalidOperationException(
                "Could not locate GTA5.exe above the selected RPF. Select an RPF inside a GTA V Legacy installation or its mods folder.");
        }

        progress?.Report("Loading encryption keys from the selected GTA V installation...");
        GTA5Keys.LoadFromPath(gameRoot);
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

    private static AwcFile LoadAwc(RpfFileEntry entry)
    {
        var bytes = entry.File.ExtractFile(entry)
            ?? throw new InvalidDataException($"Could not extract {entry.Name} from the RPF.");
        var awc = new AwcFile();
        awc.Load(bytes, entry);
        if (awc.ErrorMessage is not null)
        {
            throw new InvalidDataException($"Could not parse {entry.Name}: {awc.ErrorMessage}");
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
            throw new InvalidDataException($"The rebuilt {awcName} failed validation: {validation.ErrorMessage}");
        }
        var pair = FindStereoPair(validation)
            ?? throw new InvalidDataException($"The rebuilt {awcName} no longer has a left/right channel pair.");
        if (pair.Left.SampleCount != expectedSampleCount || pair.Right.SampleCount != expectedSampleCount)
        {
            throw new InvalidDataException($"The rebuilt {awcName} has an unexpected sample count.");
        }
    }
}
