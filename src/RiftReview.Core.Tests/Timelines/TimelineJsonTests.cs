using RiftReview.Core.Matches;
using RiftReview.Core.Timelines;

namespace RiftReview.Core.Tests.Timelines;

public sealed class TimelineJsonTests
{
    private static readonly MatchTimeline Sample = new()
    {
        GameVersion = new GameVersion(16, 20, 824, 8524),
        GameLength = TimeSpan.FromSeconds(1506.188),
        Champions = [new TimelineChampion(0, 0x400000ae, "Player One#EUW", "Jhin", Team.Blue)],
        Events = [new TimelineEvent(139.62, TimelineEventKind.Death, 0, 0), new TimelineEvent(141.4, TimelineEventKind.LevelUp, 0, 2)],
        ActivityPerMinute = [[12, 40, 7]],
    };

    [Fact]
    public void RoundTripsATimeline()
    {
        var json = TimelineJson.Write(Sample);

        Assert.True(TimelineJson.Read(json).TryGetValue(out var read, out var error), error);
        Assert.Equal(Sample.GameVersion, read.GameVersion);
        Assert.Equal(Sample.GameLength, read.GameLength);
        Assert.Equal(Sample.Champions, read.Champions);
        Assert.Equal(Sample.Events, read.Events);
        Assert.Equal([12, 40, 7], read.ActivityPerMinute[0]);
    }

    [Fact]
    public void RoundTripsTurretFallsAndTheBuilderVersion()
    {
        var built = Sample with { TurretFalls = [new TurretFall(741.234, Team.Red)], BuilderVersion = 7 };

        Assert.True(TimelineJson.Read(TimelineJson.Write(built)).TryGetValue(out var read, out var error), error);

        Assert.Equal([new TurretFall(741.23, Team.Red)], read.TurretFalls);
        Assert.Equal(7, read.BuilderVersion);
    }

    [Fact]
    public void RoundTripsTakedownsAndObjectives()
    {
        var built = Sample with
        {
            TurretFalls = [new TurretFall(741.234, Team.Red, 1, [0, 1])],
            Objectives = [new ObjectiveKill(593.456, ObjectiveKind.Dragon, Team.Blue, 1, [0, 1])],
        };

        Assert.True(TimelineJson.Read(TimelineJson.Write(built)).TryGetValue(out var read, out var error), error);

        var fall = Assert.Single(read.TurretFalls);
        Assert.Equal((741.23, Team.Red, 1), (fall.Time, fall.Owner, fall.Killer));
        Assert.Equal([0, 1], fall.Participants!);
        var dragon = Assert.Single(read.Objectives);
        Assert.Equal((593.46, ObjectiveKind.Dragon, Team.Blue, 1), (dragon.Time, dragon.Kind, dragon.Team, dragon.Killer));
        Assert.Equal([0, 1], dragon.Participants);
    }

    [Fact]
    public void IgnoresFieldsAndEventKindsItDoesNotKnow()
    {
        // A timeline from an earlier version with positions and a kind this version does not have.
        const string Stored = """{"schema":1,"gameVersion":"16.20.824.8524","gameLengthSeconds":60,"positions":[{"time":1,"champions":[[400,460,0]]}],"events":[{"time":5,"kind":"death","champion":0,"value":0}]}""";

        Assert.True(TimelineJson.Read(Stored).TryGetValue(out var read, out var error), error);

        Assert.Single(read.Events);
        Assert.Equal(0, read.BuilderVersion);
    }

    [Fact]
    public void WritesTheSchemaNumberAndReadableNames()
    {
        var json = TimelineJson.Write(Sample);

        Assert.Contains("\"schema\":1", json, StringComparison.Ordinal);
        Assert.Contains("\"kind\":\"levelUp\"", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"schema":99,"gameVersion":"16.20.824.8524","gameLengthSeconds":1}""", "schema 99")]
    [InlineData("""{"schema":1,"gameVersion":"x","gameLengthSeconds":1}""", "not a version")]
    [InlineData("""{ broken""", "not valid JSON")]
    public void RejectsWhatItCannotRead(string json, string reason)
    {
        Assert.False(TimelineJson.Read(json).TryGetValue(out _, out var error));
        Assert.Contains(reason, error, StringComparison.Ordinal);
    }
}
