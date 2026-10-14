using RiftReview.Core.Matches;

namespace RiftReview.GameData.Tests;

public sealed class DataDragonParserTests
{
    // Shapes of the real Data Dragon files, cut down to a few entries.
    private const string Champions = """
        {"type":"champion","version":"16.20.1","data":{
          "Leblanc":{"id":"Leblanc","key":"7","name":"LeBlanc"},
          "Fiddlesticks":{"id":"Fiddlesticks","key":"9","name":"Fiddlesticks"},
          "Evil":{"id":"../../evil","key":"1","name":"Evil"}}}
        """;

    private const string Items = """
        {"type":"item","version":"16.20.1","data":{"3109":{"name":"Knight's Vow"},"2003":{"name":"Health Potion"}}}
        """;

    private const string Summoners = """
        {"type":"summoner","version":"16.20.1","data":{
          "SummonerFlash":{"id":"SummonerFlash","key":"4","name":"Flash"},
          "SummonerBarrier_Jade":{"id":"SummonerBarrier_Jade","key":"721","name":"Barrier"}}}
        """;

    private const string Runes = """
        [{"id":8100,"key":"Domination","icon":"perk-images/Styles/7200_Domination.png","name":"Domination",
          "slots":[{"runes":[
            {"id":8112,"key":"Electrocute","icon":"perk-images/Styles/Domination/Electrocute/Electrocute.png","name":"Electrocute"},
            {"id":6666,"key":"Evil","icon":"../../../Windows/evil.png","name":"Evil"}]}]}]
        """;

    private static GameCatalog Catalog() => DataDragonParser.ParseCatalog("16.20.1", Champions, Items, Summoners, Runes);

    [Fact]
    public void FindsChampionsByTheReplaysNameIgnoringCase()
    {
        Assert.Equal("Fiddlesticks", Catalog().FindChampion("FiddleSticks")?.Name);
        Assert.Equal("LeBlanc", Catalog().FindChampion("Leblanc")?.Name);
        Assert.Null(Catalog().FindChampion("NotAChampion"));
    }

    [Fact]
    public void ReadsItemNamesAndSpellIconsByNumericKey()
    {
        var catalog = Catalog();

        Assert.Equal("Knight's Vow", catalog.Items[3109].Name);
        Assert.Equal("Flash", catalog.SummonerSpells[4].Name);
        Assert.Equal("cdn/16.20.1/img/spell/SummonerFlash.png", catalog.SummonerSpells[4].IconPath);
        Assert.Equal("cdn/16.20.1/img/spell/SummonerBarrier_Jade.png", catalog.SummonerSpells[721].IconPath);
    }

    [Fact]
    public void ReadsRuneTreesAndRunes()
    {
        var catalog = Catalog();

        Assert.Equal("cdn/img/perk-images/Styles/Domination/Electrocute/Electrocute.png", catalog.Runes[8112].IconPath);
        Assert.Equal("Domination", catalog.Runes[8100].Name);
    }

    [Fact]
    public void DropsIdsAndPathsThatCouldEscapeTheCache()
    {
        var catalog = Catalog();

        Assert.DoesNotContain(catalog.Champions.Values, c => c.Id.Contains('.', StringComparison.Ordinal));
        Assert.False(catalog.Runes.ContainsKey(6666));
    }

    [Fact]
    public void KeepsOnlyWellFormedVersions()
    {
        var versions = DataDragonParser.ParseVersions("""["16.20.1","16.19.1","lolpatch_7.20","../x",5]""");

        Assert.Equal(["16.20.1", "16.19.1"], versions);
    }

    [Theory]
    [InlineData(16, 20, "16.20.1")]
    [InlineData(16, 19, "16.19.2")]
    [InlineData(17, 1, "16.20.1")]
    public void PicksTheVersionOfTheReplaysPatchElseTheNewest(int major, int minor, string expected)
    {
        string[] versions = ["16.20.1", "16.19.2", "16.19.1"];

        var picked = DataDragonParser.PickVersion(versions, new GameVersion(major, minor, 0, 0));

        Assert.Equal(expected, picked);
    }

    [Fact]
    public void BuildsIconPaths()
    {
        Assert.Equal("cdn/16.20.1/img/item/3109.png", DataDragonParser.ItemIconPath("16.20.1", 3109));
        Assert.Equal("cdn/16.20.1/img/champion/Leblanc.png", DataDragonParser.ChampionIconPath("16.20.1", new ChampionInfo("Leblanc", "LeBlanc")));
    }
}
