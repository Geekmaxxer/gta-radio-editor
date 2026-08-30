namespace GTARadioEditor.Models;

public sealed record RadioStation(
    string RpfPath,
    string ArchiveCode,
    string StationName,
    string RelativePath)
{
    public string DisplayName => $"{StationName} ({ArchiveCode})";

    public string FullDisplayName => $"{DisplayName}{Environment.NewLine}{RelativePath}";
}
