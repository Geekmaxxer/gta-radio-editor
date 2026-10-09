using CodeWalker.GameFiles;
using GTARadioEditor.Models;

namespace GTARadioEditor.Services;

/// <summary>Matches streaming sounds through their audio references, never through guessed track names.</summary>
public sealed class RadioTimingService
{
    public static string? FindDefaultTimingPath(string radioRpfPath)
    {
        var audioDirectory = Directory.GetParent(Path.GetDirectoryName(radioRpfPath)!)?.FullName;
        if (audioDirectory is null) return null;
        var path = Path.Combine(audioDirectory, "audio_rel.rpf");
        return File.Exists(path) ? path : null;
    }

    internal static IEnumerable<RpfFileEntry> GetFiles(RpfFile rpf)
    {
        foreach (var entry in rpf.AllEntries.OfType<RpfFileEntry>()) yield return entry;
        foreach (var child in rpf.Children ?? [])
            foreach (var entry in GetFiles(child)) yield return entry;
    }

    internal static TimingSession Open(string timingPath, string radioPath, IReadOnlyList<RadioSlot> slots,
        IProgress<string>? progress, CancellationToken cancellationToken)
    {
        var documents = new List<TimingDocument>();
        if (timingPath.EndsWith(".dat54.rel", StringComparison.OrdinalIgnoreCase))
        {
            documents.Add(new TimingDocument(File.ReadAllBytes(timingPath), null));
        }
        else if (timingPath.EndsWith(".rpf", StringComparison.OrdinalIgnoreCase))
        {
            var archive = RpfRadioService.OpenRpf(timingPath, progress);
            foreach (var entry in GetFiles(archive).Where(e => e.NameLower.EndsWith(".dat54.rel")))
            {
                cancellationToken.ThrowIfCancellationRequested();
                documents.Add(new TimingDocument(entry.File.ExtractFile(entry)
                    ?? throw new InvalidDataException(L.T("Cannot extract {0}.", entry.Path)), entry.Path));
            }
        }
        else throw new InvalidOperationException(L.T("Select the radio timing RPF or a sounds.dat54.rel file."));

        var targets = new Dictionary<RadioSlot, List<TimingTarget>>();
        foreach (var slot in slots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // AWC stream IDs store the lower 29 bits of the waveform's Jenkins hash.
            var left = JenkHash.GenHash(slot.LeftChannelName.ToLowerInvariant()) & 0x1FFFFFFF;
            var right = JenkHash.GenHash(slot.RightChannelName.ToLowerInvariant()) & 0x1FFFFFFF;
            var relative = slot.ContainerPath.Substring(slot.ContainerPath.IndexOf('\\') + 1).Replace('\\', '/');
            var container = JenkHash.GenHash((Path.GetFileNameWithoutExtension(radioPath) + "/" +
                relative.Substring(0, relative.Length - 4)).ToLowerInvariant());
            var matches = new List<TimingTarget>();
            foreach (var document in documents)
            {
                foreach (var streaming in document.Rel.RelDatas.OfType<Dat54StreamingSound>())
                {
                    var leaves = new List<Dat54SimpleSound>();
                    var resolved = CollectLeaves(document.Rel, streaming.NameHash, new HashSet<uint>(), leaves);
                    bool IsChannel(Dat54SimpleSound sound) => sound.ContainerName == container &&
                        (((uint)sound.FileName & 0x1FFFFFFF) == left || ((uint)sound.FileName & 0x1FFFFFFF) == right);
                    if (!leaves.Any(IsChannel)) continue;
                    if (!resolved || leaves.Any(s => !IsChannel(s)) ||
                        !leaves.Any(s => ((uint)s.FileName & 0x1FFFFFFF) == left) ||
                        !leaves.Any(s => ((uint)s.FileName & 0x1FFFFFFF) == right))
                        throw new InvalidDataException(L.T("{0} uses a shared or unsupported streaming sound. Its timing cannot be changed safely.", slot.ContainerName));
                    matches.Add(new TimingTarget(document, streaming));
                }
            }
            if (matches.Count == 0)
                throw new InvalidDataException(L.T("No streaming duration matched {0} in {1}. Select the timing archive for this station/version; base stations normally use audio_rel.rpf containing sounds.dat54.rel. No output has been built.", slot.ContainerName, Path.GetFileName(timingPath)));
            targets.Add(slot, matches);
        }
        return new TimingSession(documents, targets);
    }

    private static bool CollectLeaves(RelFile rel, uint hash, HashSet<uint> ancestors, List<Dat54SimpleSound> leaves)
    {
        if (!ancestors.Add(hash) || !rel.RelDataDict.TryGetValue(hash, out var data)) return false;
        bool resolved;
        if (data is Dat54SimpleSound simple) { leaves.Add(simple); resolved = true; }
        else
        {
            var children = data.GetSoundHashes() ?? [];
            resolved = children.Length > 0;
            foreach (var child in children) resolved &= CollectLeaves(rel, child, ancestors, leaves);
        }
        ancestors.Remove(hash);
        return resolved;
    }

    internal sealed class TimingDocument
    {
        public RelFile Rel { get; } = new();
        public byte[] Bytes { get; }
        public string? EntryPath { get; }
        public Dictionary<uint, int> Durations { get; } = new();
        public TimingDocument(byte[] bytes, string? entryPath)
        {
            Bytes = bytes;
            EntryPath = entryPath;
            Rel.Load(bytes, null);
            if (Rel.RelType != RelDatFileType.Dat54DataEntries)
                throw new InvalidDataException(L.T("The selected sound metadata is not a dat54 REL file."));
        }
        public void Validate(byte[] bytes)
        {
            if (!Bytes.SequenceEqual(bytes)) throw new InvalidDataException(L.T("The written radio timing data differs from the validated patch."));
            var check = new RelFile(); check.Load(bytes, null);
            foreach (var duration in Durations)
                if (!check.RelDataDict.TryGetValue(duration.Key, out var data) ||
                    data is not Dat54StreamingSound sound || sound.Duration != duration.Value)
                    throw new InvalidDataException(L.T("A rebuilt streaming duration failed validation."));
        }
    }

    internal sealed record TimingTarget(TimingDocument Document, Dat54StreamingSound Sound);

    internal sealed class TimingSession
    {
        private readonly List<TimingDocument> _documents;
        private readonly Dictionary<RadioSlot, List<TimingTarget>> _targets;
        public TimingSession(List<TimingDocument> documents, Dictionary<RadioSlot, List<TimingTarget>> targets)
        { _documents = documents; _targets = targets; }

        public void SetDuration(RadioSlot slot, int sampleCount)
        {
            if (sampleCount <= 0) throw new InvalidDataException(L.T("Replacement audio is empty."));
            var milliseconds = checked((int)((sampleCount * 1000L + AudioConversionService.GtaSampleRate - 1) / AudioConversionService.GtaSampleRate));
            foreach (var target in _targets[slot])
            {
                // The fixed-size duration patch preserves every unknown record, index and relocation.
                var offset = checked((int)(8 + target.Sound.DataOffset + 1 + target.Sound.Header.HeaderLength));
                if (BitConverter.ToInt32(target.Document.Bytes, offset) != target.Sound.Duration)
                    throw new InvalidDataException(L.T("Streaming duration offset verification failed."));
                Array.Copy(BitConverter.GetBytes(milliseconds), 0, target.Document.Bytes, offset, 4);
                target.Sound.Duration = milliseconds;
                target.Document.Durations[target.Sound.NameHash] = milliseconds;
            }
        }

        public void Write(string path, IProgress<string>? progress)
        {
            var changed = _documents.Where(d => d.Durations.Count > 0).ToList();
            if (path.EndsWith(".dat54.rel", StringComparison.OrdinalIgnoreCase))
            {
                changed.Single().Validate(changed.Single().Bytes);
                File.WriteAllBytes(path, changed.Single().Bytes);
                changed.Single().Validate(File.ReadAllBytes(path));
                return;
            }
            var archive = RpfRadioService.OpenRpf(path, progress);
            RpfRadioService.ConvertOutputToOpenRpf(archive, progress);
            foreach (var document in changed)
            {
                document.Validate(document.Bytes);
                var entry = GetFiles(archive).Single(e => e.Path.Equals(document.EntryPath, StringComparison.OrdinalIgnoreCase));
                RpfFile.CreateFile(entry.Parent, entry.Name, document.Bytes);
            }
            archive = RpfRadioService.OpenRpf(path, progress);
            foreach (var document in changed)
            {
                var entry = GetFiles(archive).Single(e => e.Path.Equals(document.EntryPath, StringComparison.OrdinalIgnoreCase));
                document.Validate(entry.File.ExtractFile(entry));
            }
        }
    }
}
