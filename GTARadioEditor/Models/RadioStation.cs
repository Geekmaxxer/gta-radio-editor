namespace GTARadioEditor.Models;

public sealed record RadioStation(
    string RpfPath,
    string ArchiveCode,
    string StationName,
    string RelativePath,
    string? DisabledReason = null)
{
    public bool IsAvailable => string.IsNullOrWhiteSpace(DisabledReason);

    public string DisplayName => $"{StationName} ({ArchiveCode})" +
        (IsAvailable ? string.Empty : " — unavailable");

    public string FullDisplayName => IsAvailable
        ? $"{DisplayName}{Environment.NewLine}{RelativePath}"
        : $"{DisplayName}{Environment.NewLine}{DisabledReason}{Environment.NewLine}{RelativePath}";
}
