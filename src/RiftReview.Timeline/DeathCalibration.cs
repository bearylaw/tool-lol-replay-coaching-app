using RiftReview.Core.Timelines;

namespace RiftReview.Timeline;

/// <summary>
/// Deaths and respawns from packet types whose per-champion counts follow the death stat
/// (docs/research/rofl2-payload.md). Both kinds match the counts; a champion's own packets go
/// quiet after a death and resume after a respawn, which tells them apart. Each type misses a few
/// events, so deaths are anchored on respawns: every respawn has a death 3 to 130 seconds before it.
/// </summary>
internal static class DeathCalibration
{
    private const double ActivityWindow = 5;
    // Real deaths measure about 0.1, respawns 2 to 8; 1 means no activity around the event at all.
    private const double QuietAfterDeath = 1;
    private const double ActiveAfterRespawn = 1.5;
    private const double MinDeadSeconds = 3;
    private const double MaxDeadSeconds = 130;
    private const double SameDeathSeconds = 8;

    // Without a death packet before a respawn: the death is where the champion's packet rate last
    // stood at this share of its game average, in windows of this length.
    private const double ActiveShare = 0.4;
    private const double RateWindow = 2;
    private const double SearchStep = 0.5;

    /// <summary>Death and respawn events for every champion.</summary>
    /// <param name="matches">Packet types matching the death counts, each as event times per champion (deduplicated).</param>
    /// <param name="everyPacket">All packet times per champion, sorted.</param>
    /// <param name="gameEnd">Game length in seconds.</param>
    public static IEnumerable<TimelineEvent> Events(IReadOnlyList<double[][]> matches, List<float>[] everyPacket, double gameEnd)
    {
        var classified = matches.Select(m => (Times: m, Change: ActivityChange(m, everyPacket))).ToList();
        var deathTypes = classified.Where(c => c.Change <= QuietAfterDeath).Select(c => c.Times).ToList();
        var respawnTypes = classified.Where(c => c.Change > ActiveAfterRespawn).Select(c => c.Times).ToList();
        for (var hero = 0; hero < everyPacket.Length; hero++)
        {
            // One respawn type alone sends false ones (a recall, a burst of the same packet); real
            // respawns are seen by several types at once.
            var respawns = DeathCandidates([.. respawnTypes.Select(t => t[hero])])
                .Where(c => c.Support >= Math.Min(2, respawnTypes.Count))
                .Select(c => c.Time)
                .ToList();
            double? Fallback(double respawn) => ActivityDrop(everyPacket[hero], respawn, gameEnd);
            foreach (var (time, kind) in Pairs(respawns, DeathCandidates([.. deathTypes.Select(t => t[hero])]), deathTypes.Count, Fallback))
            {
                yield return new TimelineEvent(time, kind, hero, 0);
            }
        }
    }

    // Each respawn with the death before it: a death packet, else the fallback (the drop in the
    // champion's activity). A respawn with neither is not one (a recall can look alike) and is dropped.
    private static List<(double Time, TimelineEventKind Kind)> Pairs(List<double> respawns, List<Candidate> candidates, int deathTypes, Func<double, double?> fallback)
    {
        var events = new List<(double, TimelineEventKind)>();
        foreach (var respawn in respawns)
        {
            var match = candidates.LastOrDefault(c => !c.Used && c.Time > respawn - MaxDeadSeconds && c.Time < respawn - MinDeadSeconds);
            if (match is not null)
            {
                match.Used = true;
            }

            if ((match?.Time ?? fallback(respawn)) is { } death)
            {
                events.Add((death, TimelineEventKind.Death));
                events.Add((respawn, TimelineEventKind.Respawn));
            }
        }

        // Deaths without a respawn (shortly before the end, or its respawn packet is missing): several
        // death types must agree on those; one type alone sends a few false ones.
        var needed = Math.Min(2, deathTypes);
        events.AddRange(candidates.Where(c => !c.Used && c.Support >= needed).Select(c => (c.Time, TimelineEventKind.Death)));
        return events;
    }

    // One candidate per death: packets of the death types within a few seconds are the same death
    // (one type sometimes follows the others by seconds); its time is the earliest, its support the
    // number of types that saw it.
    private static List<Candidate> DeathCandidates(List<double[]> perType)
    {
        var candidates = new List<Candidate>();
        foreach (var (time, type) in perType.SelectMany((times, type) => times.Select(t => (t, type))).OrderBy(x => x.t))
        {
            if (candidates.Count > 0 && time - candidates[^1].Time < SameDeathSeconds)
            {
                candidates[^1].Types.Add(type);
            }
            else
            {
                candidates.Add(new Candidate(time, type));
            }
        }

        return candidates;
    }

    // The last moment before the respawn at which the champion was still about as active as usual;
    // null when it never went quiet in the longest possible wait.
    private static double? ActivityDrop(List<float> packets, double respawn, double gameEnd)
    {
        var active = packets.Count / Math.Max(1, gameEnd) * RateWindow * ActiveShare;
        for (var time = respawn - MinDeadSeconds; time > respawn - MaxDeadSeconds; time -= SearchStep)
        {
            if (CountBetween(packets, time - RateWindow, time) >= active)
            {
                return time < respawn - MinDeadSeconds ? time : null;
            }
        }

        return null;
    }

    // Packets of the champion in the seconds after each event over those before (1 = no change).
    private static double ActivityChange(double[][] perHero, List<float>[] everyPacket)
    {
        double before = 1, after = 1;
        for (var hero = 0; hero < perHero.Length && hero < everyPacket.Length; hero++)
        {
            foreach (var time in perHero[hero])
            {
                before += CountBetween(everyPacket[hero], time - ActivityWindow, time - 1);
                after += CountBetween(everyPacket[hero], time + 1, time + ActivityWindow);
            }
        }

        return after / before;
    }

    internal static int CountBetween(List<float> sorted, double from, double to)
    {
        var start = sorted.BinarySearch((float)from);
        var end = sorted.BinarySearch((float)to);
        return Math.Max(0, (end < 0 ? ~end : end) - (start < 0 ? ~start : start));
    }

    // Times at least a second apart: the same event can be sent twice in quick succession.
    private static List<double> Distinct(IEnumerable<double> times)
    {
        var result = new List<double>();
        foreach (var time in times.Order())
        {
            if (result.Count == 0 || time - result[^1] >= 1)
            {
                result.Add(time);
            }
        }

        return result;
    }

    private sealed class Candidate(double time, int type)
    {
        public double Time { get; } = time;

        public HashSet<int> Types { get; } = [type];

        public int Support => Types.Count;

        public bool Used { get; set; }
    }
}
