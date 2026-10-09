using GTARadioEditor.Models;

namespace GTARadioEditor.Services;

public static class TrackAssignmentService
{
    private static readonly Random Random = new();
    public static int Fill(IList<RadioSlot> slots, IReadOnlyList<AudioTrack> tracks, bool randomize)
    {
        if (slots.Count == 0 || tracks.Count == 0) return 0;
        var ordered = tracks.ToList();
        if (randomize)
        {
            for (var i = ordered.Count - 1; i > 0; i--)
            {
                var other = Random.Next(i + 1);
                (ordered[i], ordered[other]) = (ordered[other], ordered[i]);
            }
        }
        for (var i = 0; i < slots.Count; i++)
            slots[i].ReplacementPath = i < ordered.Count ? ordered[i].FilePath : null;
        return Math.Min(slots.Count, ordered.Count);
    }

    public static void ClearAll(IEnumerable<RadioSlot> slots)
    {
        foreach (var slot in slots) slot.ReplacementPath = null;
    }
}
