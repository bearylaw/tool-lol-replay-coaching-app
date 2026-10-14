using System.Globalization;
using RiftReview.Core.Matches;
using RiftReview.Core.Timelines;

namespace RiftReview.Timeline;

/// <summary>
/// When each turret fell and whose it was, from the replay file without decoding payloads
/// (docs/research/rofl2-payload.md, turrets). Structures are units present from the first seconds;
/// a destroyed one gets one packet of a rare type and then next to nothing for a minute or more (nexus
/// turrets rebuild, so they can fall twice). That type is found per replay as the one whose count on
/// structures equals the turrets the end-of-game stats say were lost. Whose turret it was comes from
/// the map's fixed structure ids (Summoner's Rift on 16.20), checked against each team's turrets
/// lost; when they do not fit (a patch renumbered them), no turret is marked rather than a wrong one.
/// </summary>
public static class TurretDecoder
{
    // A structure is there from the start; after falling it stays silent at least this long.
    private const double PresentFrom = 5;
    private const double SilentAfter = 60;
    private const double QuietWindow = 3;

    // A fallen structure can still get a stray packet or two (a rebuilding nexus turret, a last effect).
    private const int StrayPackets = 2;

    // Structure ids (low byte) of blue's turrets and red's on Summoner's Rift, 16.20 (research note).
    private static readonly HashSet<int> BlueTurrets = [0x88, 0x8b, 0x8c, 0x8d, 0x8e, 0x94, 0x9a, 0xa2, 0xa3, 0xa6, 0xab];
    private static readonly HashSet<int> RedTurrets = [0x92, 0x93, 0x96, 0x97, 0x98, 0x9b, 0xa0, 0xa1, 0xa4, 0xa5, 0xa7];

    /// <summary>The turret falls in time order, or an empty list when the pattern is not found.</summary>
    public static IReadOnlyList<TurretFall> Decode(
        IReadOnlyList<GameDataRecord> chunks, IReadOnlyList<uint> championIds, IReadOnlyList<PlayerStats> players)
    {
        ArgumentNullException.ThrowIfNull(chunks);
        ArgumentNullException.ThrowIfNull(championIds);
        ArgumentNullException.ThrowIfNull(players);
        var blueLost = Lost(players, Team.Blue);
        var redLost = Lost(players, Team.Red);
        if (championIds.Count == 0 || blueLost + redLost == 0)
        {
            return [];
        }

        var falls = Falls(chunks, championIds, blueLost + redLost);
        if (falls.Count == 0)
        {
            return [];
        }

        return ByStructureIds(falls, blueLost) is { } owners ? [.. falls.Select((f, i) => new TurretFall(f.Time, owners[i]))] : [];
    }

    // The most turrets any player of a team saw their team lose (every player has the team's count).
    private static int Lost(IReadOnlyList<PlayerStats> players, Team team) =>
        players.Where(p => p.Team == team).Select(p => int.TryParse(p.RawStats.GetValueOrDefault("FRIENDLY_TURRET_LOST"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0).DefaultIfEmpty(0).Max();

    // Of the packet types structures get, the rarest one whose falls (a packet followed by a minute
    // of silence) number exactly the turrets lost.
    private static List<(double Time, uint Unit)> Falls(IReadOnlyList<GameDataRecord> chunks, IReadOnlyList<uint> championIds, int turretsLost)
    {
        var lowest = championIds.Min();
        var highest = championIds.Max();
        var first = new Dictionary<uint, float>();
        var times = new Dictionary<uint, List<float>>();
        var byType = new Dictionary<ushort, List<(float Time, uint Unit)>>();
        var perType = new Dictionary<ushort, int>();
        foreach (var chunk in chunks)
        {
            var reader = new BlockReader(chunk.Data);
            while (reader.TryRead(out var block))
            {
                perType[block.Type] = perType.GetValueOrDefault(block.Type) + 1;
                var unit = block.Parameter;
                if (unit == 0 || (unit >= lowest && unit <= highest))
                {
                    continue;
                }

                first.TryAdd(unit, block.Time);
                if (first[unit] > PresentFrom)
                {
                    continue;
                }

                if (!times.TryGetValue(unit, out var list))
                {
                    list = [];
                    times[unit] = list;
                }

                list.Add(block.Time);
                if (!byType.TryGetValue(block.Type, out var events))
                {
                    events = [];
                    byType[block.Type] = events;
                }

                events.Add((block.Time, unit));
            }
        }

        bool SilentAfterwards((float Time, uint Unit) e) =>
            times[e.Unit].Count(t => t > e.Time + QuietWindow && t < e.Time + SilentAfter) <= StrayPackets;

        return byType
            .Where(t => t.Value.Count == turretsLost && t.Value.All(SilentAfterwards))
            .OrderBy(t => perType[t.Key])
            .Select(t => t.Value.Select(e => ((double)e.Time, e.Unit)).OrderBy(e => e.Item1).ToList())
            .FirstOrDefault() ?? [];
    }

    // Whose turret by the map's structure ids, when every fall is a known turret and the counts agree.
    private static Team[]? ByStructureIds(List<(double Time, uint Unit)> falls, int blueLost)
    {
        var owners = falls.Select(f => BlueTurrets.Contains((int)(f.Unit & 0xff)) ? Team.Blue : RedTurrets.Contains((int)(f.Unit & 0xff)) ? Team.Red : Team.Other).ToArray();
        return owners.All(o => o != Team.Other) && owners.Count(o => o == Team.Blue) == blueLost ? owners : null;
    }

}
