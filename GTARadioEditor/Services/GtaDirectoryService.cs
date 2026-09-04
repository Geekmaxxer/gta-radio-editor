using GTARadioEditor.Models;

namespace GTARadioEditor.Services;

public sealed class GtaDirectoryService
{
    private static readonly IReadOnlyDictionary<string, string> StationNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["RADIO_01_CLASS_ROCK"] = "Los Santos Rock Radio",
            ["RADIO_02_POP"] = "Non-Stop-Pop FM",
            ["RADIO_03_HIPHOP_NEW"] = "Radio Los Santos",
            ["RADIO_04_PUNK"] = "Channel X",
            ["RADIO_05_TALK_01"] = "West Coast Talk Radio",
            ["RADIO_06_COUNTRY"] = "Rebel Radio",
            ["RADIO_07_DANCE_01"] = "Soulwax FM",
            ["RADIO_08_MEXICAN"] = "East Los FM",
            ["RADIO_09_HIPHOP_OLD"] = "West Coast Classics",
            ["RADIO_11_TALK_02"] = "Blaine County Radio",
            ["RADIO_12_REGGAE"] = "Blue Ark",
            ["RADIO_13_JAZZ"] = "Worldwide FM",
            ["RADIO_14_DANCE_02"] = "FlyLo FM",
            ["RADIO_15_MOTOWN"] = "The Lowdown 91.1",
            ["RADIO_16_SILVERLAKE"] = "Radio Mirror Park",
            ["RADIO_17_FUNK"] = "Space 103.2",
            ["RADIO_18_90S_ROCK"] = "Vinewood Boulevard Radio",
            ["RADIO_19_USER"] = "Self Radio",
            ["RADIO_20_THELAB"] = "The Lab",
            ["RADIO_21_DLC_XM17"] = "Blonded Los Santos 97.8 FM",
            ["RADIO_22_DLC_BATTLE_MIX1_RADIO"] = "Los Santos Underground Radio",
            ["RADIO_23_DLC_XM19_RADIO"] = "iFruit Radio",
            ["RADIO_27_DLC_PRHEI4"] = "Still Slipping Los Santos",
            ["RADIO_34_DLC_HEI4_KULT"] = "Kult FM",
            ["RADIO_35_DLC_HEI4_MLR"] = "The Music Locker",
            ["RADIO_36_AUDIOPLAYER"] = "Media Player",
            ["RADIO_37_MOTOMAMI"] = "MOTOMAMI Los Santos"
        };

    private static readonly IReadOnlyDictionary<string, string> DisabledStationReasons =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["RADIO_05_TALK_01"] = "Talk-radio archive: it contains spoken programming, not replaceable music tracks.",
            ["RADIO_11_TALK_02"] = "Talk-radio archive: it contains spoken programming, not replaceable music tracks.",
            ["RADIO_07_DANCE_01"] = "This dance station uses an audio container layout that is not supported yet.",
            ["RADIO_14_DANCE_02"] = "This dance station uses an audio container layout that is not supported yet.",
            ["RADIO_19_USER"] = "Self Radio does not contain built-in music tracks to replace.",
            ["RADIO_36_AUDIOPLAYER"] = "Media Player does not contain built-in music tracks to replace."
        };

    public async Task<IReadOnlyList<RadioStation>> DiscoverRadioStationsAsync(
        string gtaDirectory,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(gtaDirectory))
        {
            throw new DirectoryNotFoundException("The selected GTA V folder does not exist.");
        }

        return await Task.Run(() =>
        {
            var root = Path.GetFullPath(gtaDirectory);
            var archivePaths = new List<string>();
            var directories = new Queue<string>();
            directories.Enqueue(root);
            var directoriesScanned = 0;

            while (directories.Count > 0)
            {
                var directory = directories.Dequeue();
                cancellationToken.ThrowIfCancellationRequested();
                directoriesScanned++;
                if (directoriesScanned == 1 || directoriesScanned % 100 == 0)
                {
                    progress?.Report($"Searching game folders for radio archives ({directoriesScanned} folders checked)...");
                }

                archivePaths.AddRange(EnumerateRadioArchives(directory));
                foreach (var childDirectory in EnumerateChildDirectories(directory))
                {
                    if (!IsReparsePoint(childDirectory))
                    {
                        directories.Enqueue(childDirectory);
                    }
                }
            }

            var stations = archivePaths
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(path => CreateStation(root, path))
                .Where(station => !IsNonStationArchive(station.ArchiveCode))
                .OrderBy(station => station.StationName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(station => station.ArchiveCode, StringComparer.OrdinalIgnoreCase)
                .ThenBy(station => station.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (stations.Count == 0)
            {
                throw new InvalidDataException(
                    "No RADIO_*.rpf archives were found under this folder. Select the GTA V installation or port root, not an individual audio folder.");
            }

            progress?.Report($"Found {stations.Count} radio station archive(s).");
            return (IReadOnlyList<RadioStation>)stations;
        }, cancellationToken);
    }

    private static RadioStation CreateStation(string root, string archivePath)
    {
        var archiveCode = Path.GetFileNameWithoutExtension(archivePath);
        var stationName = StationNames.TryGetValue(archiveCode, out var knownName)
            ? knownName
            : MakeFriendlyName(archiveCode);
        DisabledStationReasons.TryGetValue(archiveCode, out var disabledReason);
        return new RadioStation(archivePath, archiveCode, stationName, PlatformCompatibility.GetRelativePath(root, archivePath), disabledReason);
    }

    private static IEnumerable<string> EnumerateRadioArchives(string directory)
    {
        try
        {
            return Directory.EnumerateFiles(directory, "RADIO_*.rpf", SearchOption.TopDirectoryOnly).ToArray();
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static IEnumerable<string> EnumerateChildDirectories(string directory)
    {
        try
        {
            return Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly).ToArray();
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static bool IsReparsePoint(string directory)
    {
        try
        {
            return (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }

    private static bool IsNonStationArchive(string archiveCode) =>
        archiveCode.IndexOf("ADVERT", StringComparison.OrdinalIgnoreCase) >= 0 ||
        archiveCode.IndexOf("NEWS", StringComparison.OrdinalIgnoreCase) >= 0;

    private static string RemovePrefix(string value, string prefix) =>
        value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? value.Substring(prefix.Length) : value;

    private static string MakeFriendlyName(string archiveCode) =>
        RemovePrefix(archiveCode, "RADIO_")
            .Replace('_', ' ');
}
