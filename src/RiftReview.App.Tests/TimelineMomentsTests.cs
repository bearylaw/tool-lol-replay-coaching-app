using RiftReview.App.Overlay;
using RiftReview.Core.Matches;
using RiftReview.Core.Timelines;

namespace RiftReview.App.Tests;

public sealed class TimelineMomentsTests
{
    private static readonly string[] Names = ["Ahri", "Vi", "Syndra", "Lee Sin"];

    // Ahri and Vi are blue, Syndra and Lee Sin red.
    private static readonly MatchTimeline Sample = new()
    {
        GameVersion = GameVersion.TryParse("16.20.824.8524")!.Value,
        GameLength = TimeSpan.FromMinutes(30),
        Champions =
        [
            new TimelineChampion(0, 1, "A#1", "Ahri", Team.Blue),
            new TimelineChampion(1, 2, "B#1", "Vi", Team.Blue),
            new TimelineChampion(2, 3, "C#1", "Syndra", Team.Red),
            new TimelineChampion(3, 4, "D#1", "LeeSin", Team.Red),
        ],
        Events =
        [
            new TimelineEvent(100, TimelineEventKind.Death, 2, 0),
            new TimelineEvent(100, TimelineEventKind.Kill, 0, 2),
            new TimelineEvent(100, TimelineEventKind.Assist, 1, 2),
            new TimelineEvent(108, TimelineEventKind.Death, 1, 0),
            new TimelineEvent(108, TimelineEventKind.Kill, 3, -1),
            new TimelineEvent(300, TimelineEventKind.Death, 0, 0),
            new TimelineEvent(300, TimelineEventKind.LevelUp, 0, 6),
        ],
        ActivityPerMinute = [],
        TurretFalls = [new TurretFall(115, Team.Red, 0, [0, 1]), new TurretFall(700, Team.Blue)],
        Objectives =
        [
            new ObjectiveKill(480, ObjectiveKind.Grub, Team.Red, 3, [3]),
            new ObjectiveKill(500, ObjectiveKind.Grub, Team.Red, 3, [3, 2]),
            new ObjectiveKill(510, ObjectiveKind.Grub, Team.Red, 2, [2, 3]),
            new ObjectiveKill(900, ObjectiveKind.Dragon, Team.Blue, 1, [1, 0]),
            new ObjectiveKill(1300, ObjectiveKind.Baron, Team.Red, -1, []),
        ],
    };

    [Fact]
    public void SaysWhoKilledWhomWhoTookTurretsAndObjectives()
    {
        var moments = TimelineMoments.All(Sample, Names);

        Assert.Equal(
            [
                (100.0, "Ahri killed Syndra, with Vi", true),
                (108.0, "Lee Sin killed Vi", false),
                (115.0, "Ahri destroyed a Red turret, with Vi", true),
                (300.0, "Ahri died", false),
                (510.0, "Lee Sin took 3 voidgrubs, with Syndra", false),
                (700.0, "A Blue turret fell", false),
                (900.0, "Vi took the dragon, with Ahri", true),
                (1300.0, "Red took Baron", false),
            ],
            moments.Select(m => (m.Time, m.Text, m.IsBlue)));
    }

    [Fact]
    public void TheHoverCardShowsTheWholeFightUnderTheMouse()
    {
        var moments = TimelineMoments.All(Sample, Names);

        Assert.Equal([100.0, 108.0, 115.0], TimelineMoments.Around(moments, 109).Select(m => m.Time));
        Assert.Equal([300.0], TimelineMoments.Around(moments, 299).Select(m => m.Time));
        Assert.Empty(TimelineMoments.Around([], 10));
    }

    [Fact]
    public void ALongChainIsCutAroundTheMomentUnderTheMouse()
    {
        var moments = Enumerable.Range(0, 30).Select(i => new TimelineMoment(i * 5, $"m{i}", true)).ToList();

        var shown = TimelineMoments.Around(moments, 140);

        Assert.Equal(TimelineMoments.MostShown, shown.Count);
        Assert.Equal(145.0, shown[^1].Time);
        Assert.Contains(shown, m => m.Time == 140);
    }
}
