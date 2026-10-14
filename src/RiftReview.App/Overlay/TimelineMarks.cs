using RiftReview.App.Localization;
using RiftReview.App.Viewer;
using RiftReview.Core.Matches;
using RiftReview.Core.Timelines;

namespace RiftReview.App.Overlay;

/// <summary>What the overlay's timeline marks for objectives and for the player League has selected.</summary>
internal static class TimelineMarks
{
    /// <summary>
    /// Dragons, barons and heralds as they were taken; the grubs once, when the camp is finished, in
    /// the colour of the team that took most of it.
    /// </summary>
    public static IReadOnlyList<ObjectiveMark> Objectives(MatchTimeline? timeline)
    {
        var objectives = timeline?.Objectives ?? [];
        var marks = objectives.Where(o => o.Kind != ObjectiveKind.Grub)
            .Select(o => new ObjectiveMark(o.Time, Letter(o.Kind), o.Team == Team.Blue))
            .ToList();
        var grubs = objectives.Where(o => o.Kind == ObjectiveKind.Grub).ToList();
        if (grubs.Count > 0)
        {
            var blue = grubs.Count(g => g.Team == Team.Blue);
            marks.Add(new ObjectiveMark(grubs.Max(g => g.Time), Loc.Get("Objective_GrubLetter"), blue * 2 > grubs.Count));
        }

        return [.. marks.OrderBy(m => m.Time)];
    }

    private static string Letter(ObjectiveKind kind) => kind switch
    {
        ObjectiveKind.Dragon => Loc.Get("Objective_DragonLetter"),
        ObjectiveKind.Baron => Loc.Get("Objective_BaronLetter"),
        ObjectiveKind.Herald => Loc.Get("Objective_HeraldLetter"),
        _ => Loc.Get("Objective_GrubLetter"),
    };

    /// <summary>
    /// The selected player's deaths (their side), kills and assists (the victim's side, where that
    /// death is marked) and turret takedowns (where the turret is marked).
    /// </summary>
    public static IReadOnlyList<FocusMark> Focus(MatchTimeline? timeline, int player)
    {
        if (timeline is null || player < 0 || player >= timeline.Champions.Count)
        {
            return [];
        }

        var blue = timeline.Champions[player].Team == Team.Blue;
        bool VictimBlue(TimelineEvent e) => e.Value >= 0 && e.Value < timeline.Champions.Count ? timeline.Champions[e.Value].Team == Team.Blue : !blue;
        var marks = new List<FocusMark>();
        foreach (var e in timeline.Events.Where(e => e.Champion == player))
        {
            switch (e.Kind)
            {
                case TimelineEventKind.Death:
                    marks.Add(new FocusMark(e.Time, FocusKind.Death, blue));
                    break;
                case TimelineEventKind.Kill:
                    marks.Add(new FocusMark(e.Time, FocusKind.Kill, VictimBlue(e)));
                    break;
                case TimelineEventKind.Assist:
                    marks.Add(new FocusMark(e.Time, FocusKind.Assist, VictimBlue(e)));
                    break;
                default:
                    break;
            }
        }

        marks.AddRange(timeline.TurretFalls.Where(t => t.Participants?.Contains(player) == true).Select(t => new FocusMark(t.Time, FocusKind.Turret, t.Owner == Team.Blue)));
        return [.. marks.OrderBy(m => m.Time)];
    }
}
