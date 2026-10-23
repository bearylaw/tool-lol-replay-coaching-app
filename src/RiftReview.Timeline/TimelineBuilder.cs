using System.Globalization;
using RiftReview.Core.Matches;
using RiftReview.Core.Results;
using RiftReview.Core.Timelines;

namespace RiftReview.Timeline;

/// <summary>
/// Builds the timeline a replay file yields: the champions' unit ids; their deaths and level-ups,
/// found by calibrating packet counts against the replay's own end-of-game stats (packet ids change
/// per patch and sometimes per game; the stats do not); kills and assists; the turrets destroyed and
/// by whom; and the dragons, barons, herald and grubs taken. Only when packets
/// come and for which unit is read, never what they contain (decision 0008).
/// </summary>
public static class TimelineBuilder
{
    /// <summary>Raised whenever what a timeline gets from the file changes; stored timelines below it are rebuilt.</summary>
    public const int Version = 9;

    // A packet type is accepted as an event when its per-champion counts follow the stat this
    // closely, and its total is within this share of the stat's total.
    private const double MinCorrelation = 0.9;
    private const double MaxTotalDeviation = 0.2;

    // Death and respawn packets each miss some events (up to a fifth in server replays); they are
    // accepted more loosely because DeathCalibration cross-checks them.
    private const double MinDeathCorrelation = 0.8;
    private const double MaxDeathDeviation = 0.3;

    // The same event can be sent twice in quick succession; count it once.
    private const double DuplicateWindowSeconds = 1.0;

    /// <summary>Reads the replay's game data and builds its timeline.</summary>
    public static Result<MatchTimeline, string> Build(Stream replay, ReplayMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        var records = GameDataRecords.ReadAll(replay);
        if (!records.TryGetValue(out var all, out var error))
        {
            return Result<MatchTimeline, string>.Failure(error);
        }

        var chunks = all.Where(r => r.Kind == RecordKind.Chunk).ToList();
        var heroIds = FindChampionIds(chunks, metadata.Players.Count);
        if (heroIds is null)
        {
            return Result<MatchTimeline, string>.Success(Empty(metadata));
        }

        var scan = Scan(chunks, heroIds, metadata.GameLength);
        if (scan.Error is not null)
        {
            return Result<MatchTimeline, string>.Failure(scan.Error);
        }

        var players = metadata.Players;
        var deathMatches = Matches(scan.Times, Stat(players, p => p.Deaths), dedupe: true, MinDeathCorrelation, MaxDeathDeviation)
            .Select(m => m.PerHero.Select(t => Distinct(t).ToArray()).ToArray())
            .ToList();
        var events = new List<TimelineEvent>();
        events.AddRange(DeathCalibration.Events(deathMatches, scan.EveryPacket, metadata.GameLength.TotalSeconds));
        events.AddRange(LevelUps(scan.Times, Stat(players, p => p.Level - 1), Stat(players, p => p.Level)));

        // Kills, assists, turret takedowns and objectives come from the reward bursts; each part
        // checks itself against the stats and is left out when it does not fit.
        var ticks = Optional(() => PacketTicks.Read(chunks, heroIds, players));
        if (ticks is not null)
        {
            events.AddRange(Optional(() => KillDecoder.Decode(ticks, players, events)) ?? []);
        }

        var timeline = new MatchTimeline
        {
            GameVersion = metadata.Version,
            GameLength = metadata.GameLength,
            Champions = [.. players.Select((p, i) => new TimelineChampion(i, heroIds[i], p.RiotId, p.Champion, p.Team))],
            Events = [.. events.OrderBy(e => e.Time).ThenBy(e => e.Champion)],
            ActivityPerMinute = scan.Activity,
            BuilderVersion = Version,
        };
        var turrets = Optional(() => TurretDecoder.Decode(chunks, heroIds, players)) ?? [];
        if (ticks is not null)
        {
            turrets = Optional(() => TurretCredit.Add(turrets, ticks, players)) ?? turrets;
        }

        var objectives = ticks is null ? null : Optional(() => ObjectiveDecoder.Decode(ticks, players));
        return Result<MatchTimeline, string>.Success(timeline with { TurretFalls = turrets, Objectives = objectives ?? [] });
    }

    // The decoders read one patch's packet contents. A patch that changes them must only cost their
    // part of the timeline, never the timeline itself; each also validates its own result.
    private static T? Optional<T>(Func<T?> decode)
        where T : class
    {
        try
        {
            return decode();
        }
        catch (Exception exception) when (exception is IndexOutOfRangeException or ArgumentException or InvalidOperationException or OverflowException)
        {
            return null;
        }
    }

    /// <summary>
    /// The champions' unit ids: consecutive non-zero packet parameters in player-list order, the run
    /// with the most packets among the frequent parameters. Usually they are the most frequent ones
    /// outright; in some 16.21 replays a pet or minion outranks a quiet champion. <c>null</c> when
    /// no such run exists.
    /// </summary>
    internal static uint[]? FindChampionIds(IReadOnlyList<GameDataRecord> chunks, int playerCount)
    {
        if (playerCount == 0)
        {
            return null;
        }

        var counts = new Dictionary<uint, int>();
        foreach (var chunk in chunks)
        {
            var reader = new BlockReader(chunk.Data);
            while (reader.TryRead(out var block))
            {
                if (block.Parameter != 0)
                {
                    counts[block.Parameter] = counts.GetValueOrDefault(block.Parameter) + 1;
                }
            }
        }

        var frequent = counts.OrderByDescending(c => c.Value).Take(playerCount * 2).Select(c => c.Key).ToHashSet();
        uint[]? best = null;
        long bestPackets = 0;
        foreach (var start in frequent)
        {
            var run = Enumerable.Range(0, playerCount).Select(i => start + (uint)i).ToArray();
            var packets = run.All(frequent.Contains) ? run.Sum(id => (long)counts[id]) : 0;
            if (packets > bestPackets)
            {
                best = run;
                bestPackets = packets;
            }
        }

        return best;
    }

    private static ScanResult Scan(IReadOnlyList<GameDataRecord> chunks, uint[] heroIds, TimeSpan gameLength)
    {
        var minutes = (int)Math.Ceiling(gameLength.TotalMinutes) + 1;
        var activity = heroIds.Select(_ => new int[minutes]).ToArray();
        var everyPacket = heroIds.Select(_ => new List<float>()).ToArray();
        var times = new Dictionary<ushort, List<float>[]>();
        foreach (var chunk in chunks)
        {
            var reader = new BlockReader(chunk.Data);
            while (reader.TryRead(out var block))
            {
                var hero = block.Parameter - heroIds[0];
                if (block.Parameter < heroIds[0] || hero >= heroIds.Length)
                {
                    continue;
                }

                if (!times.TryGetValue(block.Type, out var perHero))
                {
                    perHero = [.. heroIds.Select(_ => new List<float>())];
                    times[block.Type] = perHero;
                }

                perHero[hero].Add(block.Time);
                everyPacket[hero].Add(block.Time);
                activity[hero][Math.Clamp((int)(block.Time / 60), 0, minutes - 1)]++;
            }

            if (reader.Error is not null)
            {
                return new ScanResult(times, activity, [], string.Create(CultureInfo.InvariantCulture, $"chunk {chunk.Id}: {reader.Error}"));
            }
        }

        foreach (var list in everyPacket)
        {
            list.Sort();
        }

        return new ScanResult(times, activity, everyPacket, null);
    }

    // Level-ups from packets are right to about one per champion; the level shown never passes the
    // champion's real final level from the end-of-game stats.
    private static IEnumerable<TimelineEvent> LevelUps(Dictionary<ushort, List<float>[]> times, int[] stat, int[] finalLevels)
    {
        if ((BestMatch(times, stat, dedupe: false) ?? ClosestCounts(times, stat)) is not { } perHero)
        {
            yield break;
        }

        for (var hero = 0; hero < perHero.Length; hero++)
        {
            var level = 1;
            // No duplicate filter: two level-ups within a second happen (a large experience gain).
            foreach (var time in perHero[hero].Order())
            {
                level = Math.Min(level + 1, Math.Max(finalLevels[hero], 1));
                yield return new TimelineEvent(time, TimelineEventKind.LevelUp, hero, level);
            }
        }
    }

    private static List<float>[]? BestMatch(Dictionary<ushort, List<float>[]> times, int[] stat, bool dedupe) =>
        Matches(times, stat, dedupe, MinCorrelation, MaxTotalDeviation).OrderByDescending(m => m.Correlation).Select(m => m.PerHero).FirstOrDefault();

    // Final levels differ little between players, so a level-up type a few packets short for one
    // champion can miss the correlation; it still counts when it is this close player by player.
    private const double MaxLevelShareOff = 0.08;

    private static List<float>[]? ClosestCounts(Dictionary<ushort, List<float>[]> times, int[] stat)
    {
        var best = times.Values
            .Select(perHero => (PerHero: perHero, Off: perHero.Select(t => t.Count).Zip(stat, (a, b) => Math.Abs(a - b)).Sum()))
            .OrderBy(m => m.Off)
            .FirstOrDefault();
        return best.PerHero is not null && stat.Sum() > 0 && best.Off <= stat.Sum() * MaxLevelShareOff ? best.PerHero : null;
    }

    // Packet types whose per-champion counts follow the stat closely and add up to about its total.
    private static IEnumerable<(List<float>[] PerHero, double Correlation)> Matches(Dictionary<ushort, List<float>[]> times, int[] stat, bool dedupe, double minCorrelation, double maxDeviation)
    {
        var statTotal = stat.Sum();
        if (statTotal == 0)
        {
            yield break;
        }

        foreach (var perHero in times.Values)
        {
            var counts = perHero.Select(t => dedupe ? Distinct(t).Count() : t.Count).ToArray();
            var correlation = Correlation(counts, stat);
            if (Math.Abs(counts.Sum() - statTotal) <= statTotal * maxDeviation && correlation > minCorrelation)
            {
                yield return (perHero, correlation);
            }
        }
    }

    private static IEnumerable<double> Distinct(List<float> times)
    {
        var last = double.NegativeInfinity;
        foreach (var time in times.Order())
        {
            if (time - last >= DuplicateWindowSeconds)
            {
                yield return time;
                last = time;
            }
        }
    }

    private static double Correlation(int[] a, int[] b)
    {
        var meanA = a.Average();
        var meanB = b.Average();
        double covariance = 0, varianceA = 0, varianceB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            covariance += (a[i] - meanA) * (b[i] - meanB);
            varianceA += (a[i] - meanA) * (a[i] - meanA);
            varianceB += (b[i] - meanB) * (b[i] - meanB);
        }

        return varianceA > 0 && varianceB > 0 ? covariance / Math.Sqrt(varianceA * varianceB) : 0;
    }

    private static int[] Stat(IReadOnlyList<PlayerStats> players, Func<PlayerStats, int> value) =>
        [.. players.Select(p => Math.Max(0, value(p)))];

    private static MatchTimeline Empty(ReplayMetadata metadata) => new()
    {
        GameVersion = metadata.Version,
        GameLength = metadata.GameLength,
        Champions = [],
        Events = [],
        ActivityPerMinute = [],
    };

    private sealed record ScanResult(Dictionary<ushort, List<float>[]> Times, int[][] Activity, List<float>[] EveryPacket, string? Error);
}
