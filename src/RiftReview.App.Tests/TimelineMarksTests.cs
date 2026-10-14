using RiftReview.App.Overlay;
using RiftReview.App.Viewer;
using RiftReview.Core.Matches;
using RiftReview.Core.Timelines;

namespace RiftReview.App.Tests;

public sealed class TimelineMarksTests
{
    // Champion 0 is blue, 1 red.
    private static readonly MatchTimeline Sample = new()
    {
        GameVersion = GameVersion.TryParse("16.20.824.8524")!.Value,
        GameLength = TimeSpan.FromMinutes(30),
        Champions = [new TimelineChampion(0, 1, "Northwind#EUW", "Ahri", Team.Blue), new TimelineChampion(1, 2, "Ironbark#EUW", "Syndra", Team.Red)],
        Events =
        [
            new TimelineEvent(100, TimelineEventKind.Kill, 0, 1),
            new TimelineEvent(100, TimelineEventKind.Death, 1, 0),
            new TimelineEvent(200, TimelineEventKind.Death, 0, 0),
            new TimelineEvent(300, TimelineEventKind.Assist, 0, -1),
        ],
        ActivityPerMinute = [],
        TurretFalls = [new TurretFall(400, Team.Red, 0, [0]), new TurretFall(500, Team.Blue, 1, [1])],
        Objectives =
        [
            new ObjectiveKill(510, ObjectiveKind.Grub, Team.Red, 1, [1]),
            new ObjectiveKill(520, ObjectiveKind.Grub, Team.Blue, 0, [0]),
            new ObjectiveKill(530, ObjectiveKind.Grub, Team.Blue, 0, [0]),
            new ObjectiveKill(600, ObjectiveKind.Dragon, Team.Red, 1, [1]),
        ],
    };

    [Fact]
    public void MarksTheGrubCampOnceWhenItIsFinishedForTheTeamWithMostOfIt()
    {
        var marks = TimelineMarks.Objectives(Sample);

        Assert.Equal([(530.0, "G", true), (600.0, "D", false)], marks.Select(m => (m.Time, m.Letter, m.IsBlue)));
    }

    [Fact]
    public void MarksThePlayersKillsOnTheVictimsSideAndTheirOwnTurretTakedowns()
    {
        var marks = TimelineMarks.Focus(Sample, 0);

        Assert.Equal(
            [(100.0, FocusKind.Kill, false), (200.0, FocusKind.Death, true), (300.0, FocusKind.Assist, false), (400.0, FocusKind.Turret, false)],
            marks.Select(m => (m.Time, m.Kind, m.IsBlue)));
    }
}
