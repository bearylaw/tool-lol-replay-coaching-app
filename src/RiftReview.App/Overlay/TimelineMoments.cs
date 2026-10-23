using RiftReview.App.Localization;
using RiftReview.Core.Matches;
using RiftReview.Core.Timelines;

namespace RiftReview.App.Overlay;

/// <summary>Something that happened, as one line of the timeline's hover card.</summary>
/// <param name="Time">Game time in seconds.</param>
/// <param name="Text">For example "Ahri killed Syndra, with Vi".</param>
/// <param name="IsBlue">Blue did it (got the kill, the turret or the objective).</param>
internal sealed record TimelineMoment(double Time, string Text, bool IsBlue);

/// <summary>
/// Who killed whom, who took which turret and objective, as lines for the hover card over the
/// overlay's timeline; and which of them belong to the fight under the mouse.
/// </summary>
internal static class TimelineMoments
{
    /// <summary>Moments this close in time count as one fight.</summary>
    public const double FightGap = 12;

    /// <summary>The card lists at most this many moments.</summary>
    public const int MostShown = 12;

    // A kill and the death it caused are found within this many seconds of each other.
    private const double SameKill = 1;

    // Grubs taken this close in time are one line.
    private const double SameCamp = 45;

    /// <summary>Every kill (or death without a known killer), turret and objective, in time order.</summary>
    /// <param name="timeline">The match.</param>
    /// <param name="names">The champions' names, by their index in the timeline.</param>
    public static IReadOnlyList<TimelineMoment> All(MatchTimeline? timeline, IReadOnlyList<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        if (timeline is null)
        {
            return [];
        }

        string Name(int index) => index >= 0 && index < names.Count ? names[index] : "?";
        bool IsBlue(int index) => index >= 0 && index < timeline.Champions.Count && timeline.Champions[index].Team == Team.Blue;

        var moments = new List<TimelineMoment>();
        moments.AddRange(Kills(timeline, Name, IsBlue));
        foreach (var turret in timeline.TurretFalls)
        {
            var owner = Loc.Get(turret.Owner == Team.Blue ? "Scoreboard_Blue" : "Scoreboard_Red");
            var text = turret switch
            {
                { Killer: >= 0 } => With(Loc.Format("Moment_TurretBy", Name(turret.Killer), owner), turret.Participants, turret.Killer, Name),
                { Participants.Count: > 0 } => Loc.Format("Moment_TurretTakenBy", string.Join(", ", turret.Participants.Select(Name)), owner),
                _ => Loc.Format("Moment_Turret", owner),
            };
            moments.Add(new TimelineMoment(turret.Time, text, turret.Owner != Team.Blue));
        }

        moments.AddRange(Objectives(timeline.Objectives, Name));
        return [.. moments.OrderBy(m => m.Time)];
    }

    /// <summary>
    /// The moments of the fight at <paramref name="time"/>: the nearest moment and those chained to
    /// it by gaps of at most <see cref="FightGap"/> seconds, at most <see cref="MostShown"/>.
    /// </summary>
    public static IReadOnlyList<TimelineMoment> Around(IReadOnlyList<TimelineMoment> moments, double time)
    {
        ArgumentNullException.ThrowIfNull(moments);
        if (moments.Count == 0)
        {
            return [];
        }

        var nearest = 0;
        for (var i = 1; i < moments.Count; i++)
        {
            if (Math.Abs(moments[i].Time - time) < Math.Abs(moments[nearest].Time - time))
            {
                nearest = i;
            }
        }

        var first = nearest;
        while (first > 0 && moments[first].Time - moments[first - 1].Time <= FightGap)
        {
            first--;
        }

        var last = nearest;
        while (last < moments.Count - 1 && moments[last + 1].Time - moments[last].Time <= FightGap)
        {
            last++;
        }

        // A long chain is cut around the moment under the mouse.
        first = Math.Max(first, Math.Min(nearest - (MostShown / 2), last - MostShown + 1));
        return [.. moments.Skip(first).Take(Math.Min(last - first + 1, MostShown))];
    }

    private static IEnumerable<TimelineMoment> Kills(MatchTimeline timeline, Func<int, string> name, Func<int, bool> isBlue)
    {
        var kills = timeline.Events.Where(e => e.Kind == TimelineEventKind.Kill).ToList();
        var assists = timeline.Events.Where(e => e.Kind == TimelineEventKind.Assist).ToList();
        foreach (var death in timeline.Events.Where(e => e.Kind == TimelineEventKind.Death))
        {
            var victim = death.Champion;
            bool Near(TimelineEvent e) => Math.Abs(e.Time - death.Time) <= SameKill;
            var kill = kills.FirstOrDefault(k => Near(k) && k.Value == victim)
                ?? kills.FirstOrDefault(k => Near(k) && k.Value < 0 && isBlue(k.Champion) != isBlue(victim));
            if (kill is null)
            {
                yield return new TimelineMoment(death.Time, Loc.Format("Moment_Died", name(victim)), !isBlue(victim));
                continue;
            }

            var helpers = assists.Where(a => Near(a) && (a.Value == victim || (a.Value < 0 && isBlue(a.Champion) == isBlue(kill.Champion))))
                .Select(a => a.Champion).Distinct().ToList();
            var text = With(Loc.Format("Moment_Kill", name(kill.Champion), name(victim)), helpers, kill.Champion, name);
            yield return new TimelineMoment(death.Time, text, isBlue(kill.Champion));
        }
    }

    private static IEnumerable<TimelineMoment> Objectives(IReadOnlyList<ObjectiveKill> objectives, Func<int, string> name)
    {
        foreach (var objective in objectives.Where(o => o.Kind != ObjectiveKind.Grub))
        {
            yield return Objective(objective.Time, objective.Team, objective.Killer, objective.Participants, ObjectiveName(objective.Kind), name);
        }

        // Grubs of one camp, taken by one team, are one line: "Lee Sin took 3 voidgrubs".
        var grubs = objectives.Where(o => o.Kind == ObjectiveKind.Grub).OrderBy(o => o.Time).ToList();
        var camp = new List<ObjectiveKill>();
        foreach (var grub in grubs.Append(null))
        {
            if (grub is not null && (camp.Count == 0 || (grub.Team == camp[0].Team && grub.Time - camp[^1].Time <= SameCamp)))
            {
                camp.Add(grub);
                continue;
            }

            if (camp.Count > 0)
            {
                var killer = camp.Where(g => g.Killer >= 0).GroupBy(g => g.Killer).OrderByDescending(g => g.Count()).FirstOrDefault()?.Key ?? -1;
                var what = camp.Count == 1 ? Loc.Get("Moment_Grub") : Loc.Format("Moment_Grubs", camp.Count);
                yield return Objective(camp[^1].Time, camp[0].Team, killer, [.. camp.SelectMany(g => g.Participants).Distinct()], what, name);
            }

            camp = grub is null ? [] : [grub];
        }
    }

    private static TimelineMoment Objective(double time, Team team, int killer, IReadOnlyList<int> participants, string what, Func<int, string> name)
    {
        var text = killer >= 0
            ? With(Loc.Format("Moment_ObjectiveBy", name(killer), what), participants, killer, name)
            : Loc.Format("Moment_ObjectiveBy", Loc.Get(team == Team.Blue ? "Scoreboard_Blue" : "Scoreboard_Red"), what);
        return new TimelineMoment(time, text, team == Team.Blue);
    }

    // "…, with Vi, Lee Sin" for the helpers other than the one named first.
    private static string With(string text, IReadOnlyList<int>? helpers, int named, Func<int, string> name)
    {
        var others = (helpers ?? []).Where(h => h != named).Select(name).ToList();
        return others.Count == 0 ? text : Loc.Format("Moment_With", text, string.Join(", ", others));
    }

    private static string ObjectiveName(ObjectiveKind kind) => kind switch
    {
        ObjectiveKind.Dragon => Loc.Get("Moment_Dragon"),
        ObjectiveKind.Baron => Loc.Get("Moment_Baron"),
        _ => Loc.Get("Moment_Herald"),
    };
}
