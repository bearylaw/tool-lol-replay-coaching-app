namespace RiftReview.Core.Matches;

/// <summary>What a replay file says about its match, read without decoding the game data.</summary>
public sealed record ReplayMetadata
{
    /// <summary>The game build that recorded the replay; only this build can play it.</summary>
    public required GameVersion Version { get; init; }

    /// <summary>Length of the match.</summary>
    public required TimeSpan GameLength { get; init; }

    /// <summary>The ten (or more, in other modes) players with their end-of-game stats.</summary>
    public required IReadOnlyList<PlayerStats> Players { get; init; }

    /// <summary>The team that won, or <see cref="Team.Other"/> when the replay does not say.</summary>
    public Team WinningTeam =>
        Players.FirstOrDefault(p => p.Won && p.Team != Team.Other)?.Team ?? Team.Other;

    /// <summary>The players on one side, in Riot's order (top, jungle, mid, bottom, support).</summary>
    public IEnumerable<PlayerStats> PlayersOn(Team team) => Players.Where(p => p.Team == team);
}
