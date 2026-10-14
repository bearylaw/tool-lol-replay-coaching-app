using RiftReview.Core.Matches;
using RiftReview.Core.Timelines;

namespace RiftReview.Timeline;

/// <summary>
/// Dragons, barons, the herald and the voidgrubs taken, from the replay file (docs/research/rofl2-payload.md,
/// epic monsters). A monster is a unit created a few seconds before its spawn timer with a burst of
/// packets and many kinds of them; it dies when it gets the death type, found per replay as the type a
/// long-lived unit gets once at a fixed delay before it is removed. Later dragons and barons are the
/// unit created one respawn timer after the last died that most resembles the first. In the death tick
/// the last hit is the first champion paid the killer type, the takedowns are its team paid the
/// participant type; both types are found against the end-of-game stats.
/// </summary>
internal static class ObjectiveDecoder
{
    // Summoner's Rift's timers on 16.20, as windows for when the monster's unit is created (it is
    // created before it appears). Game rules, not packet ids: check them when Riot changes the map.
    private static readonly (double From, double To) FirstDragon = (290, 296);
    private static readonly (double From, double To) NextDragon = (284, 297);
    private static readonly (double From, double To) Elder = (344, 357);
    private static readonly (double From, double To) Grubs = (470, 477);
    private static readonly (double From, double To) Herald = (893, 899);
    private static readonly (double From, double To) FirstBaron = (1176, 1182);
    private static readonly (double From, double To) NextBaron = (330, 345);

    private const int MonsterKinds = 15;
    private const int LivingKinds = 10;
    private const double SameMonster = 0.6;
    private const int MaxDragons = 12;
    private const double GrubCampSpread = 30;

    private sealed record Unit(uint Id, float First, int Packets, int Kinds, float Death, HashSet<ushort> Types);

    /// <summary>The objectives taken in time order; empty when the pattern is not found.</summary>
    public static IReadOnlyList<ObjectiveKill> Decode(PacketTicks ticks, IReadOnlyList<PlayerStats> players)
    {
        if (DeathType(ticks.Units) is not { } death)
        {
            return [];
        }

        var units = ticks.Units.Select(u => ToUnit(u.Key, u.Value, death)).Where(u => !float.IsNaN(u.Death) && u.Kinds >= LivingKinds).ToList();
        var found = Monsters(units);
        if (found.Count == 0)
        {
            return [];
        }

        var lastHits = PacketTicks.Stat(players, "DRAGON_KILLS", "ELDER_DRAGON_KILLS", "BARON_KILLS", "RIFT_HERALD_KILLS", "HORDE_KILLS");
        var types = ticks.ChampionTypes();
        var killerType = types.OrderBy(t => PacketTicks.Distance(Killers(ticks, found, t, players.Count), lastHits)).FirstOrDefault();
        if (PacketTicks.Distance(Killers(ticks, found, killerType, players.Count), lastHits) > Math.Max(1, lastHits.Sum() / 4))
        {
            return [];
        }

        var takedowns = PacketTicks.Stat(players, "Missions_TakedownDragons", "Missions_TakedownBaronsElderDragons");
        ushort? participantType = takedowns.Sum() == 0
            ? null
            : types.OrderBy(t => PacketTicks.Distance(Participants(ticks, found, killerType, t, players.Count), takedowns)).First();
        return [.. found.Select(f => Take(ticks, f.Kind, f.Unit, killerType, participantType)).OfType<ObjectiveKill>().OrderBy(o => o.Time)];
    }

    // The type sent once to a long-lived unit at the same delay before its last packet, most often.
    private static ushort? DeathType(Dictionary<uint, List<UnitPacket>> units)
    {
        var tally = new Dictionary<(ushort Type, int Delay), int>();
        foreach (var packets in units.Values)
        {
            if (packets.Count < LivingKinds || packets[^1].Time - packets[0].Time < 10 || packets.Select(p => p.Type).Distinct().Count() < LivingKinds)
            {
                continue;
            }

            var last = packets[^1].Time;
            foreach (var once in packets.GroupBy(p => p.Type).Where(g => g.Count() == 1).Select(g => g.First()))
            {
                var gap = last - once.Time;
                if (gap is >= 1 and <= 10)
                {
                    var key = (once.Type, (int)Math.Round(gap * 10));
                    tally[key] = tally.GetValueOrDefault(key) + 1;
                }
            }
        }

        return tally.Count == 0 ? null : tally.MaxBy(t => t.Value).Key.Type;
    }

    private static Unit ToUnit(uint id, List<UnitPacket> packets, ushort death)
    {
        var dies = packets.FindLast(p => p.Type == death);
        return new Unit(id, packets[0].Time, packets.Count, packets.Select(p => p.Type).Distinct().Count(), dies.Type == death ? dies.Time : float.NaN, [.. packets.Select(p => p.Type)]);
    }

    private static List<(ObjectiveKind Kind, Unit Unit)> Monsters(List<Unit> units)
    {
        var found = new List<(ObjectiveKind, Unit)>();
        found.AddRange(Chain(units, FirstDragon, MaxDragons, d => [NextDragon, Elder]).Select(u => (ObjectiveKind.Dragon, u)));
        found.AddRange(GrubCamp(units).Select(u => (ObjectiveKind.Grub, u)));
        if (Biggest(units, Herald) is { } herald)
        {
            found.Add((ObjectiveKind.Herald, herald));
        }

        found.AddRange(Chain(units, FirstBaron, MaxDragons, b => [NextBaron]).Select(u => (ObjectiveKind.Baron, u)));
        return found;
    }

    // The first monster of a kind, then each next one: created a respawn timer after the last died,
    // and most like the first.
    private static List<Unit> Chain(List<Unit> units, (double From, double To) first, int max, Func<Unit, (double From, double To)[]> respawns)
    {
        var chain = new List<Unit>();
        if (Biggest(units, first) is not { } anchor)
        {
            return chain;
        }

        chain.Add(anchor);
        for (var last = anchor; chain.Count < max;)
        {
            var next = respawns(last).Select(w => Like(units, anchor, last.Death + w.From, last.Death + w.To)).FirstOrDefault(u => u is not null);
            if (next is null)
            {
                break;
            }

            chain.Add(next);
            last = next;
        }

        return chain;
    }

    private static Unit? Biggest(List<Unit> units, (double From, double To) window) =>
        units.Where(u => u.Kinds >= MonsterKinds && u.First >= window.From && u.First <= window.To).MaxBy(u => u.Packets);

    private static Unit? Like(List<Unit> units, Unit anchor, double from, double to) =>
        units.Where(u => u.Id != anchor.Id && u.First >= from && u.First <= to)
            .Select(u => (Unit: u, Similarity: (double)u.Types.Intersect(anchor.Types).Count() / u.Types.Union(anchor.Types).Count()))
            .Where(u => u.Similarity >= SameMonster)
            .OrderByDescending(u => u.Similarity)
            .Select(u => u.Unit)
            .FirstOrDefault();

    // The grubs are created together in one tick; a camp's units have neighbouring even ids, and one
    // can show up in the data before the others.
    private static List<Unit> GrubCamp(List<Unit> units)
    {
        var camp = units.Where(u => u.First >= Grubs.From && u.First <= Grubs.To).GroupBy(u => u.First).Where(g => g.Count() >= 2).MaxBy(g => g.Average(u => u.Kinds));
        if (camp is null)
        {
            return [];
        }

        var byId = units.Where(u => u.Kinds >= MonsterKinds).ToDictionary(u => u.Id);
        var ids = camp.Select(u => u.Id).ToHashSet();
        for (var grew = true; grew;)
        {
            grew = false;
            foreach (var near in ids.ToList().SelectMany(id => new[] { id - 2, id + 2 }))
            {
                if (!ids.Contains(near) && byId.TryGetValue(near, out var unit) && Math.Abs(unit.First - camp.Key) < GrubCampSpread)
                {
                    grew |= ids.Add(near);
                }
            }
        }

        return [.. units.Where(u => ids.Contains(u.Id))];
    }

    private static ObjectiveKill? Take(PacketTicks ticks, ObjectiveKind kind, Unit unit, ushort killerType, ushort? participantType)
    {
        var tick = ticks.At(unit.Death);
        var killer = tick.Where(p => p.Type == killerType).Select(p => p.Hero).DefaultIfEmpty(-1).First();
        if (killer < 0)
        {
            return null;
        }

        var team = ticks.Teams[killer];
        var participants = participantType is { } type
            ? tick.Where(p => p.Type == type && ticks.Teams[p.Hero] == team).Select(p => p.Hero).Append(killer).Distinct().Order().ToArray()
            : [killer];
        return new ObjectiveKill(unit.Death, kind, team, killer, participants);
    }

    private static int[] Killers(PacketTicks ticks, List<(ObjectiveKind Kind, Unit Unit)> found, ushort type, int players)
    {
        var counts = new int[players];
        foreach (var take in found.Select(f => Take(ticks, f.Kind, f.Unit, type, null)).OfType<ObjectiveKill>())
        {
            counts[take.Killer]++;
        }

        return counts;
    }

    // Takedowns are only in the stats for dragons and barons (elder counts with barons).
    private static int[] Participants(PacketTicks ticks, List<(ObjectiveKind Kind, Unit Unit)> found, ushort killerType, ushort type, int players)
    {
        var counts = new int[players];
        foreach (var take in found.Where(f => f.Kind is ObjectiveKind.Dragon or ObjectiveKind.Baron).Select(f => Take(ticks, f.Kind, f.Unit, killerType, type)).OfType<ObjectiveKill>())
        {
            foreach (var hero in take.Participants)
            {
                counts[hero]++;
            }
        }

        return counts;
    }
}
