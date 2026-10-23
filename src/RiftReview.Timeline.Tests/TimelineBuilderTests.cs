using RiftReview.Core.Rofl;
using RiftReview.Core.Timelines;

namespace RiftReview.Timeline.Tests;

public sealed class TimelineBuilderTests
{
    private const uint Blue = 0x400000ae;
    private const uint Red = 0x400000af;
    private const ushort Movement = 0x02d3;
    private const ushort Death = 0x00a6;
    private const ushort LevelUp = 0x03c9;
    private const ushort Distractor = 0x0111;
    private const ushort Respawn = 0x009e;

    [Fact]
    public void FindsTheChampionsAndTheirDeathsFromTheStats()
    {
        var chunk = Activity()
            .Full(100f, Death, Blue)
            .Full(250f, Death, Blue)
            .Full(250.4f, Death, Blue)
            .Full(300f, Death, Red)
            .Full(310f, Distractor, Blue)
            .Full(320f, Distractor, Red)
            .Full(330f, Distractor, Red);

        var timeline = Build(chunk, blueDeaths: 2, redDeaths: 1, blueLevel: 1, redLevel: 1);

        Assert.Equal([Blue, Red], timeline.Champions.Select(c => c.NetworkId));
        Assert.Equal(["Blue#EUW", "Red#EUW"], timeline.Champions.Select(c => c.RiotId));
        var deaths = timeline.Events.Where(e => e.Kind == TimelineEventKind.Death).ToList();
        Assert.Equal([(100.0, 0), (250.0, 0), (300.0, 1)], deaths.Select(e => (Math.Round(e.Time, 1), e.Champion)));
    }

    [Fact]
    public void TellsDeathsFromRespawnsByWhatTheChampionDoesAfterwards()
    {
        // Both packet types follow the death counts; the champion is quiet between them.
        var dead = new Dictionary<uint, (float From, float To)[]>
        {
            [Blue] = [(50, 70), (150, 172)],
            [Red] = [(100, 121)],
        };
        var chunk = new Blocks();
        for (var second = 1; second < 200; second++)
        {
            foreach (var (unit, windows) in dead)
            {
                if (!windows.Any(w => second > w.From && second < w.To))
                {
                    chunk.Full(second + 0.5f, Movement, unit);
                }
            }
        }

        foreach (var (unit, windows) in dead)
        {
            foreach (var (from, to) in windows)
            {
                chunk.Full(from, Death, unit).Full(to, Respawn, unit);
            }
        }

        var timeline = Build(chunk, blueDeaths: 2, redDeaths: 1, blueLevel: 1, redLevel: 1);

        Assert.Equal([(50.0, 0), (100.0, 1), (150.0, 0)], Times(timeline, TimelineEventKind.Death));
        Assert.Equal([(70.0, 0), (121.0, 1), (172.0, 0)], Times(timeline, TimelineEventKind.Respawn));
    }

    [Fact]
    public void NumbersLevelUpsUpToTheFinalLevelEvenBeyondEighteen()
    {
        // Regression: levels were capped at 18, but top laners reach 20 through the role quest.
        var chunk = Activity();
        for (var i = 0; i < 19; i++)
        {
            chunk.Full(60f + (i * 60), LevelUp, Blue);
        }

        for (var i = 0; i < 3; i++)
        {
            chunk.Full(60f + (i * 60), LevelUp, Red);
        }

        var timeline = Build(chunk, blueDeaths: 0, redDeaths: 0, blueLevel: 20, redLevel: 4);

        var blueLevels = timeline.Events.Where(e => e.Kind == TimelineEventKind.LevelUp && e.Champion == 0).Select(e => e.Value).ToList();
        Assert.Equal(Enumerable.Range(2, 19), blueLevels);
    }

    [Fact]
    public void NeverShowsALevelAboveTheFinalOne()
    {
        var chunk = Activity();
        for (var i = 0; i < 6; i++)
        {
            chunk.Full(60f + (i * 60), LevelUp, Blue);
        }

        for (var i = 0; i < 2; i++)
        {
            chunk.Full(60f + (i * 60), LevelUp, Red);
        }

        var timeline = Build(chunk, blueDeaths: 0, redDeaths: 0, blueLevel: 6, redLevel: 3);

        Assert.All(timeline.Events.Where(e => e.Champion == 0), e => Assert.True(e.Value <= 6));
    }

    [Fact]
    public void LeavesEventsOutWhenNoPacketTypeMatchesTheStats()
    {
        var chunk = Activity().Full(100f, Death, Blue);

        var timeline = Build(chunk, blueDeaths: 5, redDeaths: 7, blueLevel: 1, redLevel: 1);

        Assert.DoesNotContain(timeline.Events, e => e.Kind == TimelineEventKind.Death);
    }

    [Fact]
    public void CountsActivityPerMinute()
    {
        var timeline = Build(Activity(), blueDeaths: 0, redDeaths: 0, blueLevel: 1, redLevel: 1);

        Assert.Equal(40, timeline.ActivityPerMinute[0][0]);
        Assert.Equal(30, timeline.ActivityPerMinute[1][0]);
    }

    [Fact]
    public void FindsAQuietChampionBehindABusierUnit()
    {
        // Regression (16.21): a pet with more packets than the red champion hid the champions.
        var chunk = Activity();
        for (var i = 0; i < 50; i++)
        {
            chunk.Full(1f + (i * 0.1f), Movement, Red + 40);
        }

        var timeline = Build(chunk, blueDeaths: 0, redDeaths: 0, blueLevel: 1, redLevel: 1);

        Assert.Equal([Blue, Red], timeline.Champions.Select(c => c.NetworkId));
    }

    [Fact]
    public void KeepsLevelUpsAFewShortForOneChampion()
    {
        // Regression (16.21): levels vary little between players, so a type a packet short for one
        // champion missed the correlation and all level-ups were dropped.
        var chunk = Activity();
        for (var i = 0; i < 12; i++)
        {
            chunk.Full(60f + (i * 60), LevelUp, Blue);
        }

        for (var i = 0; i < 11; i++)
        {
            chunk.Full(60f + (i * 60), LevelUp, Red);
        }

        var timeline = Build(chunk, blueDeaths: 0, redDeaths: 0, blueLevel: 13, redLevel: 13);

        Assert.Equal(12, timeline.Events.Count(e => e.Kind == TimelineEventKind.LevelUp && e.Champion == 0));
        Assert.Equal(11, timeline.Events.Count(e => e.Kind == TimelineEventKind.LevelUp && e.Champion == 1));
    }

    [Fact]
    public void ReturnsNoChampionsWhenTheUnitIdsAreNotConsecutive()
    {
        var chunk = new Blocks();
        for (var i = 0; i < 20; i++)
        {
            chunk.Full(1f, Movement, Blue).Full(1f, Movement, Red + 5);
        }

        var timeline = Build(chunk, blueDeaths: 0, redDeaths: 0, blueLevel: 1, redLevel: 1);

        Assert.Empty(timeline.Champions);
        Assert.Empty(timeline.Events);
    }

    private static (double, int)[] Times(MatchTimeline timeline, TimelineEventKind kind) =>
        [.. timeline.Events.Where(e => e.Kind == kind).Select(e => (Math.Round(e.Time, 1), e.Champion)).Order()];

    // Movement packets make the two champions the most frequent units, as in real replays.
    private static Blocks Activity()
    {
        var blocks = new Blocks();
        for (var i = 0; i < 30; i++)
        {
            blocks.Full(1f + i, Movement, Blue).Full(1f + i, Movement, Red);
        }

        for (var i = 0; i < 10; i++)
        {
            blocks.Full(2f + i, Movement, Blue).Full(2f, Movement, 0x40001234);
        }

        return blocks;
    }

    private static MatchTimeline Build(Blocks chunk, int blueDeaths, int redDeaths, int blueLevel, int redLevel)
    {
        using var file = new SyntheticReplay()
            .WithRecord(RecordKind.InitialKeyframe, 1, [0x00])
            .WithRecord(RecordKind.Chunk, 3, chunk.ToArray())
            .WithRecord(RecordKind.Keyframe, 1, new Blocks().Full(1f, Death, Blue).ToArray())
            .WithPlayer("Blue", 100, blueDeaths, blueLevel)
            .WithPlayer("Red", 200, redDeaths, redLevel)
            .Build();
        Assert.True(RoflReader.Read(file).TryGetValue(out var metadata, out var error), error?.Detail);
        Assert.True(TimelineBuilder.Build(file, metadata).TryGetValue(out var timeline, out var failure), failure);
        return timeline;
    }
}
