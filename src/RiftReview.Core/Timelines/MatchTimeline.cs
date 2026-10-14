using RiftReview.Core.Matches;

namespace RiftReview.Core.Timelines;

/// <summary>What happened at a moment of a match.</summary>
public enum TimelineEventKind
{
    /// <summary>A champion died.</summary>
    Death,

    /// <summary>
    /// A champion reached a new level; <see cref="TimelineEvent.Value"/> is the level. From the replay
    /// file this is approximate (about two level-ups per champion off).
    /// </summary>
    LevelUp,

    /// <summary>A dead champion came back to life in its fountain.</summary>
    Respawn,
}

/// <summary>One champion of the match, in the order of the replay's player list.</summary>
/// <param name="Index">Position in the player list, 0 to 9.</param>
/// <param name="NetworkId">The unit id the game uses for this champion in the replay's packets.</param>
/// <param name="RiotId">The player's Riot ID.</param>
/// <param name="Champion">Riot's internal champion name.</param>
/// <param name="Team">The side the champion played on.</param>
public sealed record TimelineChampion(int Index, uint NetworkId, string RiotId, string Champion, Team Team);

/// <summary>An event of the match.</summary>
/// <param name="Time">Seconds since the game started.</param>
/// <param name="Kind">What happened.</param>
/// <param name="Champion">Index of the champion in <see cref="MatchTimeline.Champions"/>.</param>
/// <param name="Value">Kind-specific value, for example the new level; 0 when unused.</param>
public sealed record TimelineEvent(double Time, TimelineEventKind Kind, int Champion, int Value);

/// <summary>A turret destroyed, found in the replay file.</summary>
/// <param name="Time">Seconds since the game started.</param>
/// <param name="Owner">The team whose turret it was (the other team destroyed it).</param>
public sealed record TurretFall(double Time, Team Owner);

/// <summary>
/// The patch-independent record of a match (decision 0005): built once from the replay file and
/// stored as versioned JSON so it outlives the patch.
/// </summary>
public sealed record MatchTimeline
{
    /// <summary>The schema written to disk; bump it and add a migration step in <see cref="TimelineJson"/>.</summary>
    public const int CurrentSchema = 1;

    /// <summary>The build that recorded the replay.</summary>
    public required GameVersion GameVersion { get; init; }

    /// <summary>Length of the match.</summary>
    public required TimeSpan GameLength { get; init; }

    /// <summary>The champions; empty when the replay has no player list.</summary>
    public required IReadOnlyList<TimelineChampion> Champions { get; init; }

    /// <summary>Events in time order.</summary>
    public required IReadOnlyList<TimelineEvent> Events { get; init; }

    /// <summary>Packets per champion per minute of game time, a measure of how much each champion did.</summary>
    public required IReadOnlyList<IReadOnlyList<int>> ActivityPerMinute { get; init; }

    /// <summary>Turrets destroyed, in time order; empty when the file did not yield them.</summary>
    public IReadOnlyList<TurretFall> TurretFalls { get; init; } = [];

    /// <summary>
    /// The version of the file reader that built the timeline; a stored timeline from an older
    /// reader is rebuilt from the file.
    /// </summary>
    public int BuilderVersion { get; init; }
}
