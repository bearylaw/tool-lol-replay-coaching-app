using RiftReview.Core.Matches;
using RiftReview.Core.Timelines;

namespace RiftReview.Timeline.Tests;

/// <summary>
/// Kill and turret credit from synthetic reward bursts shaped like 16.20's: in a kill's tick the
/// victim gets the mark first, each credited champion a reward, the killer last its reward and then
/// the mark.
/// </summary>
public sealed class RewardBurstTests
{
    private const uint First = 0x40000100;
    private const ushort Reward = 0x2bb;
    private const ushort Mark = 0x1b5;
    private const ushort Gold = 0x332;
    private const ushort Noise = 0x0aa;

    // Champions 0 and 1 are blue, 2 and 3 red.
    private static readonly Team[] Teams = [Team.Blue, Team.Blue, Team.Red, Team.Red];

    [Fact]
    public void FindsKillerVictimAndAssistersFromTheBurstOrder()
    {
        var blocks = Background();
        // 100 s: champion 0 kills 2, champion 1 assists. 200 s: champion 3 kills 0 alone.
        Burst(blocks, 100, (2, Mark), (1, Reward), (0, Reward), (0, Mark));
        Burst(blocks, 200, (0, Mark), (3, Reward), (3, Mark));
        var ticks = Ticks(blocks);
        var players = Players(kills: [1, 0, 0, 1], assists: [0, 1, 0, 0]);

        var events = KillDecoder.Decode(ticks, players, []);

        Assert.Equal(
            [(100.0, TimelineEventKind.Kill, 0, 2), (100.0, TimelineEventKind.Assist, 1, 2), (200.0, TimelineEventKind.Kill, 3, 0)],
            events.Select(e => (Math.Round(e.Time, 1), e.Kind, e.Champion, e.Value)));
    }

    [Fact]
    public void CreditsATurretToTheFirstAttackerPaidAndItsTakedowns()
    {
        var blocks = Background();
        // A red turret falls at 300 s: champion 1 gets the last hit, 0 helped; at 400 s minions take one.
        Burst(blocks, 300, (1, Gold), (0, Reward), (1, Reward), (2, Noise));
        Burst(blocks, 400, (2, Noise));
        var ticks = Ticks(blocks);
        var players = Players(kills: [0, 0, 0, 0], assists: [0, 0, 0, 0], turretsKilled: [0, 1, 0, 0], turretTakedowns: [1, 1, 0, 0]);

        var falls = TurretCredit.Add([new TurretFall(300, Team.Red), new TurretFall(400, Team.Red)], ticks, players);

        Assert.Equal(1, falls[0].Killer);
        Assert.Equal([0, 1], falls[0].Participants);
        Assert.Equal(-1, falls[1].Killer);
    }

    [Fact]
    public void CreditsTheParticipantsWhenTheReplayHasNoKillerType()
    {
        // As in some 16.21 replays: the burst pays the takedowns but no type marks the last hits.
        var blocks = Background();
        Burst(blocks, 300, (0, Reward), (1, Reward), (2, Noise));
        Burst(blocks, 400, (1, Reward), (2, Noise));
        var ticks = Ticks(blocks);
        var players = Players(kills: [0, 0, 0, 0], assists: [0, 0, 0, 0], turretsKilled: [3, 3, 0, 0], turretTakedowns: [1, 2, 0, 0]);

        var falls = TurretCredit.Add([new TurretFall(300, Team.Red), new TurretFall(400, Team.Red)], ticks, players);

        Assert.All(falls, f => Assert.Equal(-1, f.Killer));
        Assert.Equal([0, 1], falls[0].Participants);
        Assert.Equal([1], falls[1].Participants);
    }

    [Fact]
    public void LeavesKillsOutWhenNoTypeFitsTheStats()
    {
        var ticks = Ticks(Background());

        Assert.Empty(KillDecoder.Decode(ticks, Players(kills: [3, 0, 0, 2], assists: [0, 0, 0, 0]), []));
    }

    // Some packets for everyone, so no type matches by accident.
    private static Blocks Background()
    {
        var blocks = new Blocks();
        for (var second = 1; second < 50; second++)
        {
            for (var hero = 0u; hero < 4; hero++)
            {
                blocks.Full(second, Noise, First + hero);
            }
        }

        return blocks;
    }

    private static void Burst(Blocks blocks, float time, params (int Hero, ushort Type)[] packets)
    {
        foreach (var (hero, type) in packets)
        {
            blocks.Full(time, type, First + (uint)hero);
        }
    }

    private static PacketTicks Ticks(Blocks blocks) =>
        PacketTicks.Read([new GameDataRecord(RecordKind.Chunk, 1, blocks.ToArray())], [First, First + 1, First + 2, First + 3], Players([0, 0, 0, 0], [0, 0, 0, 0]));

    private static PlayerStats[] Players(int[] kills, int[] assists, int[]? turretsKilled = null, int[]? turretTakedowns = null) =>
        [.. Enumerable.Range(0, 4).Select(i => new PlayerStats
        {
            GameName = $"P{i}",
            TagLine = "EUW",
            Champion = "Annie",
            Team = Teams[i],
            Position = Position.Middle,
            Won = Teams[i] == Team.Blue,
            RawStats = new Dictionary<string, string>
            {
                ["CHAMPIONS_KILLED"] = $"{kills[i]}",
                ["ASSISTS"] = $"{assists[i]}",
                ["TURRETS_KILLED"] = $"{turretsKilled?[i] ?? 0}",
                ["TURRET_TAKEDOWNS"] = $"{turretTakedowns?[i] ?? 0}",
            },
        })];
}
