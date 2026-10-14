using System.Globalization;
using RiftReview.App.Localization;
using RiftReview.Core.Matches;

namespace RiftReview.App.Replays;

/// <summary>One side of the scoreboard with its totals.</summary>
internal sealed class TeamViewModel
{
    public TeamViewModel(Team team, IReadOnlyList<PlayerRowViewModel> players)
    {
        Team = team;
        Players = players;
        Won = players.Count > 0 && players[0].Stats.Won;
        Name = team == Team.Blue ? Loc.Get("Team_Blue") : Loc.Get("Team_Red");
        Result = players.Count == 0 ? string.Empty : Won ? Loc.Get("Result_Victory") : Loc.Get("Result_Defeat");

        var stats = players.Select(p => p.Stats).ToList();
        Kills = stats.Sum(p => p.Kills);
        Deaths = stats.Sum(p => p.Deaths);
        Assists = stats.Sum(p => p.Assists);
        Gold = stats.Sum(p => (long)p.GoldEarned);
        Damage = stats.Sum(p => p.DamageToChampions);
        Towers = SumRaw(stats, "TURRETS_KILLED");
        Inhibitors = SumRaw(stats, "BARRACKS_KILLED");
        Dragons = SumRaw(stats, "DRAGON_KILLS");
        Barons = SumRaw(stats, "BARON_KILLS");
        Grubs = SumRaw(stats, "HORDE_KILLS");
        Heralds = SumRaw(stats, "RIFT_HERALD_KILLS");
        VisionScore = stats.Sum(p => p.VisionScore);
    }

    public Team Team { get; }

    public bool IsBlue => Team == Team.Blue;

    public string Name { get; }

    public bool Won { get; }

    public string Result { get; }

    public IReadOnlyList<PlayerRowViewModel> Players { get; }

    public int Kills { get; }

    public int Deaths { get; }

    public int Assists { get; }

    public long Gold { get; }

    public long Damage { get; }

    public int Towers { get; }

    public int Inhibitors { get; }

    public int Dragons { get; }

    public int Barons { get; }

    public int Grubs { get; }

    public int Heralds { get; }

    public int VisionScore { get; }

    public string KillsDeathsAssists => $"{Kills} / {Deaths} / {Assists}";

    public string GoldText => MatchFormat.Thousands(Gold);

    private static int SumRaw(IEnumerable<PlayerStats> players, string key) =>
        players.Sum(p => int.TryParse(p.RawStats.GetValueOrDefault(key), NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : 0);
}
