using System.Text.Json;
using RiftReview.Core.Matches;
using RiftReview.Core.Rofl;
using static RiftReview.Core.Tests.Rofl.RoflFileBuilder;

namespace RiftReview.Core.Tests.Rofl;

public sealed class PlayerStatsParserTests
{
    [Fact]
    public void MapsRiotStatNamesToTypedStats()
    {
        var player = ParseOne(APlayer("Northwind", "Jhin", 100, won: true));

        Assert.Equal("Northwind#EUW", player.RiotId);
        Assert.Equal("Jhin", player.Champion);
        Assert.Equal(Team.Blue, player.Team);
        Assert.Equal(Position.Middle, player.Position);
        Assert.True(player.Won);
        Assert.Equal((9, 7, 11), (player.Kills, player.Deaths, player.Assists));
        Assert.Equal(213, player.CreepScore);
        Assert.Equal([3115, 0, 0, 0, 0, 0, 3363], player.Items);
        Assert.Equal([4, 12], player.SummonerSpells);
        Assert.Equal(8112, player.Keystone);
    }

    [Fact]
    public void KeepsEveryRawStat()
    {
        var stats = APlayer("Northwind", "Jhin", 100, won: true);
        stats["SOME_FUTURE_STAT"] = "77";

        Assert.Equal("77", ParseOne(stats).RawStats["SOME_FUTURE_STAT"]);
    }

    [Fact]
    public void ReadsAMissingNumberAsZero()
    {
        var stats = APlayer("Northwind", "Jhin", 100, won: true);
        stats.Remove("ASSISTS");

        Assert.Equal(0, ParseOne(stats).Assists);
    }

    [Fact]
    public void RejectsANumberThatIsNotANumber()
    {
        var stats = APlayer("Northwind", "Jhin", 100, won: true);
        stats["CHAMPIONS_KILLED"] = "lots";

        var error = ParseError(stats);

        Assert.Contains("CHAMPIONS_KILLED", error, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsAPlayerWithoutAChampion()
    {
        var stats = APlayer("Northwind", "Jhin", 100, won: true);
        stats.Remove("SKIN");

        Assert.Contains("SKIN", ParseError(stats), StringComparison.Ordinal);
    }

    [Fact]
    public void FallsBackToTheSummonerNameWithoutARiotId()
    {
        var stats = APlayer("ignored", "Jhin", 100, won: true);
        stats.Remove("RIOT_ID_GAME_NAME");
        stats.Remove("RIOT_ID_TAG_LINE");
        stats["NAME"] = "OldName";

        Assert.Equal("OldName", ParseOne(stats).RiotId);
    }

    [Theory]
    [InlineData("200", Team.Red)]
    [InlineData("300", Team.Other)]
    public void MapsTeamIds(string teamId, Team expected)
    {
        var stats = APlayer("Northwind", "Jhin", 100, won: true);
        stats["TEAM"] = teamId;

        Assert.Equal(expected, ParseOne(stats).Team);
    }

    [Fact]
    public void TreatsAnUnknownPositionAsNone()
    {
        var stats = APlayer("Northwind", "Jhin", 100, won: true);
        stats["TEAM_POSITION"] = "";

        Assert.Equal(Position.None, ParseOne(stats).Position);
    }

    private static PlayerStats ParseOne(Dictionary<string, string> stats)
    {
        var result = PlayerStatsParser.Parse(JsonSerializer.Serialize(new[] { stats }));
        Assert.True(result.TryGetValue(out var players, out var error), error);
        return Assert.Single(players);
    }

    private static string ParseError(Dictionary<string, string> stats)
    {
        var result = PlayerStatsParser.Parse(JsonSerializer.Serialize(new[] { stats }));
        Assert.False(result.TryGetValue(out _, out var error));
        return error;
    }
}
