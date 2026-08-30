namespace GTARadioEditor.Models;

public sealed class RadioSlot
{
    public required string ContainerName { get; init; }
    public required string ContainerPath { get; init; }
    public required string LeftChannelName { get; init; }
    public required string RightChannelName { get; init; }
    public int SampleRate { get; init; }
    public TimeSpan OriginalDuration { get; init; }
    public string? ReplacementPath { get; set; }

    public string ReplacementDisplay => ReplacementPath is null
        ? "Drop a song here"
        : Path.GetFileName(ReplacementPath);
}

public sealed record AudioTrack(string FilePath, TimeSpan Duration)
{
    public string DisplayName => Path.GetFileName(FilePath);
}

public sealed record ConvertedRadioAudio(byte[] LeftWave, byte[] RightWave, int SampleCount, TimeSpan Duration);

public sealed record BuildResult(string OutputRpfPath, int ReplacedContainers, IReadOnlyList<string> Messages);
