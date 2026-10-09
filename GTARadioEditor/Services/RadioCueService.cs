using CodeWalker.GameFiles;

namespace GTARadioEditor.Services;

public static class RadioCueService
{
    public static void Retime(AwcFile awc, int originalSamples, int replacementSamples)
    {
        if (originalSamples <= 0 || replacementSamples <= 0)
            throw new InvalidDataException("Radio audio must contain samples before its cues can be retimed.");
        foreach (var stream in awc.Streams)
        {
            foreach (var marker in stream.MarkersChunk?.Markers ?? [])
            {
                if (marker.SampleOffset > originalSamples)
                    throw new InvalidDataException("This AWC contains a cue beyond the original song. Its cue layout is unsupported.");
                marker.SampleOffset = (uint)Math.Min(replacementSamples,
                    (marker.SampleOffset * (long)replacementSamples + originalSamples / 2L) / originalSamples);
            }
        }
    }
}
