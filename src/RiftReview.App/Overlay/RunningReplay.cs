using RiftReview.Capture;
using RiftReview.Core.Matches;

namespace RiftReview.App.Overlay;

/// <summary>
/// A replay or spectated game open in League right now, as the game's local APIs describe it: its
/// length (for a spectated game, up to now) and the players' Riot IDs, with the players' live data;
/// both empty when the Live Client Data API did not answer.
/// </summary>
internal sealed record RunningReplay(double LengthSeconds, IReadOnlyList<string> RiotIds, LiveSnapshot? Snapshot = null)
{
    // The replay runs a few seconds past the game's end, so lengths agree only this closely.
    private const double SameLengthSeconds = 20;

    /// <summary>Whether a match of this length can be the running replay.</summary>
    public bool HasLength(TimeSpan gameLength) => Math.Abs(LengthSeconds - gameLength.TotalSeconds) <= SameLengthSeconds;

    /// <summary>
    /// The library replay that is running: same length and at least half of the running players
    /// in its player list, the most shared players winning. Without players from League, a length
    /// alone decides only when exactly one replay has it. <c>null</c> when none fits.
    /// </summary>
    public T? FindIn<T>(IEnumerable<T> replays, Func<T, ReplayMetadata?> metadata)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(metadata);
        var sameLength = replays.Where(r => metadata(r) is { } m && HasLength(m.GameLength)).ToList();
        if (RiotIds.Count == 0)
        {
            return sameLength.Count == 1 ? sameLength[0] : null;
        }

        var running = RiotIds.Select(GameName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var needed = (running.Count + 1) / 2;
        return sameLength
            .Select(r => (Replay: r, Shared: metadata(r)!.Players.Count(p => running.Contains(p.GameName.Trim()))))
            .Where(c => c.Shared >= needed)
            .OrderByDescending(c => c.Shared)
            .ThenBy(c => Math.Abs(LengthSeconds - metadata(c.Replay)!.GameLength.TotalSeconds))
            .Select(c => c.Replay)
            .FirstOrDefault();
    }

    // League reports Name#Tag; the replay's tag can be missing, so only the name part is compared.
    private static string GameName(string riotId)
    {
        var hash = riotId.IndexOf('#', StringComparison.Ordinal);
        return (hash < 0 ? riotId : riotId[..hash]).Trim();
    }
}
