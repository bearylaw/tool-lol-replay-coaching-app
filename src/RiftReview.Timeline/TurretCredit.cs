using RiftReview.Core.Matches;
using RiftReview.Core.Timelines;

namespace RiftReview.Timeline;

/// <summary>
/// Who destroyed each turret, from the reward burst in the tick it fell (docs/research/rofl2-payload.md):
/// the last hit is the first attacker to get the killer type, the takedowns are the attackers that get
/// the participant type, and the killer. Both types are found per replay against the end-of-game
/// turret kills and takedowns. A fall without a killer was the minions'.
/// </summary>
internal static class TurretCredit
{
    // A type is used only when it matches the stats at least this well (share of their total off).
    private const double MaxShareOff = 0.35;

    /// <summary>The falls with killer and participants added where the stats confirm the types.</summary>
    public static IReadOnlyList<TurretFall> Add(IReadOnlyList<TurretFall> falls, PacketTicks ticks, IReadOnlyList<PlayerStats> players)
    {
        var killed = PacketTicks.Stat(players, "TURRETS_KILLED");
        var takedowns = PacketTicks.Stat(players, "TURRET_TAKEDOWNS");
        if (falls.Count == 0 || killed.Sum() == 0)
        {
            return falls;
        }

        var bursts = falls.Select(f => (Fall: f, Attackers: ticks.At(f.Time).Where(p => ticks.Teams[p.Hero] != f.Owner).ToArray())).ToList();
        var types = ticks.ChampionTypes();
        var killerType = Best(types, t => Killers(bursts, t, players.Count), killed);
        if (killerType is null)
        {
            return falls;
        }

        var participantType = Best(types, t => Participants(bursts, killerType.Value, t, players.Count), takedowns);
        return [.. bursts.Select(b =>
        {
            var killer = KillerOf(b.Attackers, killerType.Value);
            return b.Fall with
            {
                Killer = killer,
                Participants = participantType is { } p ? [.. TakedownsOf(b.Attackers, killer, p).Order()] : null,
            };
        })];
    }

    private static ushort? Best(IReadOnlyList<ushort> types, Func<ushort, int[]> counts, int[] stat)
    {
        var best = types.Select(t => (Type: t, Off: PacketTicks.Distance(counts(t), stat))).OrderBy(t => t.Off).FirstOrDefault();
        return stat.Sum() > 0 && best.Off <= Math.Max(2, stat.Sum() * MaxShareOff) ? best.Type : null;
    }

    private static int KillerOf(ChampionPacket[] attackers, ushort killerType) =>
        attackers.Where(p => p.Type == killerType).Select(p => p.Hero).DefaultIfEmpty(-1).First();

    private static HashSet<int> TakedownsOf(ChampionPacket[] attackers, int killer, ushort participantType)
    {
        var set = attackers.Where(p => p.Type == participantType).Select(p => p.Hero).ToHashSet();
        if (killer >= 0)
        {
            set.Add(killer);
        }

        return set;
    }

    private static int[] Killers(List<(TurretFall Fall, ChampionPacket[] Attackers)> bursts, ushort type, int players)
    {
        var counts = new int[players];
        foreach (var killer in bursts.Select(b => KillerOf(b.Attackers, type)).Where(k => k >= 0))
        {
            counts[killer]++;
        }

        return counts;
    }

    private static int[] Participants(List<(TurretFall Fall, ChampionPacket[] Attackers)> bursts, ushort killerType, ushort type, int players)
    {
        var counts = new int[players];
        foreach (var hero in bursts.SelectMany(b => TakedownsOf(b.Attackers, KillerOf(b.Attackers, killerType), type)))
        {
            counts[hero]++;
        }

        return counts;
    }
}
