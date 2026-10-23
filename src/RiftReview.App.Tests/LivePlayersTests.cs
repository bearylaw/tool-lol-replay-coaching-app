using RiftReview.App.Overlay;
using RiftReview.Capture;
using RiftReview.Core.Matches;

namespace RiftReview.App.Tests;

public sealed class LivePlayersTests
{
    [Fact]
    public void SpectatedPlayersBecomeReplayPlayersBlueFirst()
    {
        var snapshot = new LiveSnapshot(600, [Live("Emberfall#EUW", "Ahri", isBlue: false, "Flash", "Ignite"), Live("Northwind", "MonkeyKing", isBlue: true, "Flash", "Unleashed Smite")]);
        var spells = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Flash"] = 4, ["Ignite"] = 14 };

        var players = LivePlayers.From(snapshot, spells);

        Assert.Equal(["Northwind", "Emberfall"], players.Select(p => p.GameName));
        Assert.Equal(["", "EUW"], players.Select(p => p.TagLine));
        Assert.Equal([Team.Blue, Team.Red], players.Select(p => p.Team));
        Assert.Equal("MonkeyKing", players[0].Champion);
        Assert.Equal([4, 0], players[0].SummonerSpells);
        Assert.Equal([4, 14], players[1].SummonerSpells);
    }

    private static LivePlayer Live(string riotId, string champion, bool isBlue, params string[] spells) =>
        new(riotId, champion, isBlue, 1, false, 0, 0, 0, 0, 0, 0, [], spells);
}
