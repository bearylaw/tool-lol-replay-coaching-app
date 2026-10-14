namespace RiftReview.Capture.Tests;

public sealed class LiveGameDataTests
{
    // Shape of /liveclientdata/allgamedata during a replay, cut down to one player.
    private const string AllGameData = """
        {"activePlayer":{"error":"Spectator mode doesn't currently support this feature"},
         "allPlayers":[{"championName":"Gangplank","isDead":true,"respawnTimer":12.5,"level":7,
           "riotId":"Northwind#EUW","team":"ORDER",
           "items":[{"itemID":1055,"slot":0},{"itemID":3340,"slot":6}],
           "summonerSpells":{"summonerSpellOne":{"displayName":"Unleashed Smite"},"summonerSpellTwo":{"displayName":"Flash"}},
           "scores":{"assists":2,"creepScore":64,"deaths":1,"kills":3,"wardScore":4.5}}],
         "events":{"Events":[]},
         "gameData":{"gameMode":"CLASSIC","gameTime":431.25,"mapNumber":11}}
        """;

    private const string Events = """
        {"Events":[
          {"EventID":0,"EventName":"GameStart","EventTime":0.0},
          {"EventID":5,"EventName":"ChampionKill","EventTime":312.5,"KillerName":"Northwind","VictimName":"Ashen Vale","Assisters":["Riverstone"]},
          {"EventID":6,"EventName":"DragonKill","EventTime":600.1,"KillerName":"Emberfall","DragonType":"Earth","Stolen":"False","Assisters":[]},
          {"EventID":7,"EventName":"Multikill","EventTime":700,"KillerName":"Northwind","KillStreak":3}]}
        """;

    [Fact]
    public void ReadsPlayersFromAllGameData()
    {
        Assert.True(LiveGameDataParser.ParseAllGameData(AllGameData).TryGetValue(out var snapshot, out var error), error);

        Assert.Equal(431.25, snapshot.GameTime);
        var player = Assert.Single(snapshot.Players);
        Assert.Equal("Northwind#EUW", player.RiotId);
        Assert.True(player.IsBlue);
        Assert.True(player.IsDead);
        Assert.Equal(12.5, player.RespawnSeconds);
        Assert.Equal((3, 1, 2, 64), (player.Kills, player.Deaths, player.Assists, player.CreepScore));
        Assert.Equal([new LiveItem(1055, 0), new LiveItem(3340, 6)], player.Items);
        Assert.Equal(["Unleashed Smite", "Flash"], player.SummonerSpellNames);
    }

    [Fact]
    public void ReadsTheEventFeed()
    {
        Assert.True(LiveGameDataParser.ParseEvents(Events).TryGetValue(out var events, out var error), error);

        Assert.Equal(4, events.Count);
        Assert.Equal(("Northwind", "Ashen Vale"), (events[1].Killer, events[1].Victim));
        Assert.Equal(["Riverstone"], events[1].Assisters);
        Assert.Equal("Earth", events[2].Detail);
        Assert.Equal("3", events[3].Detail);
    }

    [Theory]
    [InlineData("""{"allPlayers":[]}""")]
    [InlineData("""{ nope""")]
    public void ExplainsUnreadableGameData(string json)
    {
        Assert.False(LiveGameDataParser.ParseAllGameData(json).TryGetValue(out _, out var error));
        Assert.NotEmpty(error);
    }
}
