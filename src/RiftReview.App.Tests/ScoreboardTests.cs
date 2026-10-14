using System.Globalization;
using RiftReview.App.Overlay;
using RiftReview.Core.Matches;
using RiftReview.Core.Timelines;

namespace RiftReview.App.Tests;

public sealed class ScoreboardTests
{
    [Fact]
    public void GoldReadsLikeLeagues()
    {
        Assert.Equal("950", Scoreboard.Gold(950));
        Assert.Equal(4.2.ToString("0.0", CultureInfo.CurrentCulture) + "k", Scoreboard.Gold(4200));
    }

    [Fact]
    public void TeamTotalsCountOnlyWhatHappenedUpToNow()
    {
        // Red lost a turret at 10:00 (blue took it), blue one at 15:00.
        var board = new Scoreboard([], [new TurretFall(600, Team.Red), new TurretFall(900, Team.Blue)]);
        ObjectiveTaken[] taken = [new(500, "DragonKill", true), new(1300, "BaronKill", false)];

        board.Update(700, taken);

        Assert.StartsWith(Totals(towers: 1, dragons: 1, barons: 0), board.Blue.Totals, StringComparison.Ordinal);
        Assert.StartsWith(Totals(towers: 0, dragons: 0, barons: 0), board.Red.Totals, StringComparison.Ordinal);

        board.Update(1400, taken);

        Assert.StartsWith(Totals(towers: 1, dragons: 0, barons: 1), board.Red.Totals, StringComparison.Ordinal);
    }

    // The totals line without its item gold.
    private static string Totals(int towers, int dragons, int barons) =>
        Localization.Loc.Format("Scoreboard_Totals", 0, towers, dragons, barons, "#").Split('#')[0];
}
