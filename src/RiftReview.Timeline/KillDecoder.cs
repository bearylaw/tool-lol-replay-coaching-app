using RiftReview.Core.Matches;
using RiftReview.Core.Timelines;

namespace RiftReview.Timeline;

/// <summary>
/// Who killed whom and who assisted, from the reward burst of each kill (docs/research/rofl2-payload.md):
/// in the kill's tick every credited champion gets a reward packet, and the killer, paid last, then
/// gets a mark packet the victim got first. The reward and mark types are found per replay as the
/// pair whose killers best match the end-of-game kills; the assist type likewise against assists.
/// </summary>
internal static class KillDecoder
{
    // A victim without its own mark in the burst is the enemy whose death is nearest within this.
    private const double VictimWindow = 1.5;

    // Rejected when the best calibration is further off the stats than this share of their total.
    private const double MaxShareOff = 0.25;

    /// <summary>Kill and assist events, or none when no packet types fit the stats.</summary>
    public static IReadOnlyList<TimelineEvent> Decode(PacketTicks ticks, IReadOnlyList<PlayerStats> players, IReadOnlyList<TimelineEvent> deaths)
    {
        var kills = PacketTicks.Stat(players, "CHAMPIONS_KILLED");
        var assists = PacketTicks.Stat(players, "ASSISTS");
        if (kills.Sum() == 0 || Calibrate(ticks, kills) is not { } pair)
        {
            return [];
        }

        var assistType = ticks.ChampionTypes()
            .Where(type => ticks.Count(type) >= assists.Sum())
            .Select(type => (Type: type, Off: PacketTicks.Distance(Count(Find(ticks, pair, type), k => k.Assisters, assists.Length), assists)))
            .OrderBy(t => t.Off)
            .FirstOrDefault();
        ushort? assist = assistType.Off <= Math.Max(2, assists.Sum() * MaxShareOff) ? assistType.Type : null;
        var events = new List<TimelineEvent>();
        foreach (var kill in Find(ticks, pair, assist))
        {
            var victim = Victim(kill, ticks.Teams, deaths);
            events.Add(new TimelineEvent(kill.Time, TimelineEventKind.Kill, kill.Killer, victim));
            events.AddRange(kill.Assisters.Select(a => new TimelineEvent(kill.Time, TimelineEventKind.Assist, a, victim)));
        }

        return events;
    }

    private sealed record Kill(double Time, int Killer, int[] Assisters, int[] MarkedFirst);

    // The (reward, mark) pair: the killer gets the mark after its reward in the same tick.
    private static (ushort Reward, ushort Mark)? Calibrate(PacketTicks ticks, int[] kills)
    {
        var total = kills.Sum();
        var marks = ticks.ChampionTypes().Where(t => ticks.Count(t) >= 0.5 * total && ticks.Count(t) <= 8 * Math.Max(total, 5)).ToHashSet();
        var pairs = new Dictionary<(ushort, ushort), int>();
        foreach (var tick in ticks.Ticks)
        {
            for (var j = 0; j < tick.Length; j++)
            {
                if (!marks.Contains(tick[j].Type))
                {
                    continue;
                }

                var before = new HashSet<ushort>();
                for (var k = 0; k < j; k++)
                {
                    if (tick[k].Hero == tick[j].Hero && tick[k].Type != tick[j].Type && before.Add(tick[k].Type))
                    {
                        pairs[(tick[k].Type, tick[j].Type)] = pairs.GetValueOrDefault((tick[k].Type, tick[j].Type)) + 1;
                    }
                }
            }
        }

        var best = pairs.Where(p => p.Value >= 0.5 * total && p.Value <= 4 * total)
            .Select(p => (Pair: p.Key, Off: PacketTicks.Distance(Count(Find(ticks, p.Key, null), k => [k.Killer], kills.Length), kills)))
            .OrderBy(p => p.Off)
            .FirstOrDefault();
        return best.Pair != default && best.Off <= total * MaxShareOff ? best.Pair : null;
    }

    // Each tick with the mark: per team, the champions who got the mark after a reward; the last of
    // them is the killer (the killer is paid last). Champions with the mark before any reward died.
    private static List<Kill> Find(PacketTicks ticks, (ushort Reward, ushort Mark) pair, ushort? assist)
    {
        var found = new List<Kill>();
        foreach (var i in ticks.TicksWith(pair.Mark))
        {
            var tick = ticks.Ticks[i];
            var rewarded = new HashSet<int>();
            var candidates = new List<int>();
            var markedFirst = new HashSet<int>();
            foreach (var packet in tick)
            {
                if (packet.Type == pair.Reward)
                {
                    rewarded.Add(packet.Hero);
                }
                else if (packet.Type == pair.Mark)
                {
                    (rewarded.Contains(packet.Hero) ? candidates : (ICollection<int>)markedFirst).Add(packet.Hero);
                }
            }

            foreach (var team in candidates.Select(h => ticks.Teams[h]).Distinct())
            {
                var killer = candidates.Last(h => ticks.Teams[h] == team);
                var assisters = assist is { } type
                    ? tick.Where(p => p.Type == type && p.Hero != killer && ticks.Teams[p.Hero] == team).Select(p => p.Hero).Distinct().Order().ToArray()
                    : [];
                found.Add(new Kill(ticks.Times[i], killer, assisters, [.. markedFirst.Where(h => ticks.Teams[h] != team)]));
            }
        }

        return found;
    }

    private static int Victim(Kill kill, Team[] teams, IReadOnlyList<TimelineEvent> deaths)
    {
        if (kill.MarkedFirst.Length == 1)
        {
            return kill.MarkedFirst[0];
        }

        var near = deaths
            .Where(d => d.Kind == TimelineEventKind.Death && teams[d.Champion] != teams[kill.Killer] && Math.Abs(d.Time - kill.Time) < VictimWindow)
            .Where(d => kill.MarkedFirst.Length == 0 || kill.MarkedFirst.Contains(d.Champion))
            .OrderBy(d => Math.Abs(d.Time - kill.Time))
            .FirstOrDefault();
        return near?.Champion ?? -1;
    }

    private static int[] Count(List<Kill> kills, Func<Kill, IEnumerable<int>> who, int players)
    {
        var counts = new int[players];
        foreach (var hero in kills.SelectMany(who).Where(h => h >= 0 && h < counts.Length))
        {
            counts[hero]++;
        }

        return counts;
    }
}
