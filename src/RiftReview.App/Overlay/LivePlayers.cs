using RiftReview.Capture;
using RiftReview.Core.Matches;

namespace RiftReview.App.Overlay;

/// <summary>The players of a game known only from League's live data, such as one spectated live.</summary>
internal static class LivePlayers
{
    /// <summary>
    /// The players in League's order (blue first) as replay players with no end-of-game stats yet:
    /// Riot ID, champion, side and the summoner spells whose display names the catalog knows.
    /// </summary>
    public static IReadOnlyList<PlayerStats> From(LiveSnapshot snapshot, IReadOnlyDictionary<string, int> spellIdByName)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(spellIdByName);
        return [.. snapshot.Players
            .OrderByDescending(p => p.IsBlue)
            .Select(p =>
            {
                var hash = p.RiotId.IndexOf('#', StringComparison.Ordinal);
                return new PlayerStats
                {
                    GameName = hash < 0 ? p.RiotId : p.RiotId[..hash],
                    TagLine = hash < 0 ? string.Empty : p.RiotId[(hash + 1)..],
                    Champion = p.Champion,
                    Team = p.IsBlue ? Team.Blue : Team.Red,
                    Position = Position.None,
                    Won = false,
                    SummonerSpells = [.. p.SummonerSpellNames.Select(n => spellIdByName.GetValueOrDefault(n))],
                };
            })];
    }
}
