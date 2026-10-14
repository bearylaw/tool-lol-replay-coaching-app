namespace RiftReview.Library.Tests;

public sealed class ReplaySearchTests : IDisposable
{
    private readonly TempLibrary library = new();
    private readonly ReplayEntry entry;

    public ReplaySearchTests()
    {
        var path = library.WriteReplay(
            "players/Northwind/EUW1-9.rofl",
            new DateTime(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc),
            ("Northwind", "Jhin"),
            ("Riverstone", "Rell"));
        entry = ReplayLibrary.ReadEntry(path, new LibraryFolder(library.Path("players"), ReplaySource.Players));
    }

    public void Dispose() => library.Dispose();

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    [InlineData("jhin")]
    [InlineData("northwind#euw")]
    [InlineData("rell riverstone")]
    [InlineData("16.20")]
    [InlineData("EUW1-9")]
    public void FindsAReplayByAnyOfItsWords(string? query)
    {
        Assert.True(ReplaySearch.Matches(entry, query));
    }

    [Theory]
    [InlineData("ahri")]
    [InlineData("jhin ahri")]
    public void RequiresEveryWord(string query)
    {
        Assert.False(ReplaySearch.Matches(entry, query));
    }
}
