using System.Globalization;
using RiftReview.App.Localization;
using RiftReview.App.Mvvm;
using RiftReview.Core.Matches;
using RiftReview.Core.Timelines;

namespace RiftReview.App.Overlay;

/// <summary>One team on the scoreboard: its players and what it has taken so far.</summary>
internal sealed class ScoreboardTeam(bool isBlue, IReadOnlyList<OverlayPlayer> players) : ObservableObject
{
    private string totals = string.Empty;

    public bool IsBlue { get; } = isBlue;

    public string Name { get; } = isBlue ? Loc.Get("Scoreboard_Blue") : Loc.Get("Scoreboard_Red");

    public IReadOnlyList<OverlayPlayer> Players { get; } = players;

    /// <summary>Kills, towers and item gold, as one line.</summary>
    public string Totals
    {
        get => totals;
        private set => SetProperty(ref totals, value);
    }

    public void Update(int towers)
    {
        var kills = Players.Sum(p => p.Kills);
        var gold = Players.Sum(p => p.ItemValue);
        Totals = Loc.Format("Scoreboard_Totals", kills, towers, Scoreboard.Gold(gold));
    }
}

/// <summary>
/// The overlay's scoreboard: both teams with level, K/D/A, CS, items, ward score and the gold value of
/// their items, at the replay's current time (League's Live Client Data), and per team the kills and
/// towers (from the replay file) so far. Dragons and barons stay with League's score bar: its event
/// feed lists only what played since the replay opened, so it misses objectives skipped by a seek.
/// </summary>
internal sealed class Scoreboard : ObservableObject
{
    private readonly IReadOnlyList<TurretFall> turretFalls;
    private bool isOpen;

    public Scoreboard(IReadOnlyList<OverlayPlayer> players, IReadOnlyList<TurretFall> turretFalls)
    {
        this.turretFalls = turretFalls;
        Blue = new ScoreboardTeam(true, [.. players.Where(p => p.IsBlue)]);
        Red = new ScoreboardTeam(false, [.. players.Where(p => !p.IsBlue)]);
        ToggleCommand = new RelayCommand(() => IsOpen = !IsOpen);
    }

    public ScoreboardTeam Blue { get; }

    public ScoreboardTeam Red { get; }

    public IReadOnlyList<ScoreboardTeam> Teams => [Blue, Red];

    public System.Windows.Input.ICommand ToggleCommand { get; }

    public bool IsOpen
    {
        get => isOpen;
        set => SetProperty(ref isOpen, value);
    }

    /// <summary>Recomputes the team totals for the replay time <paramref name="now"/>.</summary>
    public void Update(double now)
    {
        // A turret falls to the other team.
        Blue.Update(turretFalls.Count(t => t.Time <= now && t.Owner == Team.Red));
        Red.Update(turretFalls.Count(t => t.Time <= now && t.Owner == Team.Blue));
    }

    /// <summary>Gold as League writes it: 950, 4.2k, 12.3k.</summary>
    internal static string Gold(int gold) => gold < 1000
        ? gold.ToString(CultureInfo.CurrentCulture)
        : (gold / 1000.0).ToString("0.0", CultureInfo.CurrentCulture) + "k";
}
