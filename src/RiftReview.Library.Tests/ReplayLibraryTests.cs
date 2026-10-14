using RiftReview.Core.Rofl;

namespace RiftReview.Library.Tests;

public sealed class ReplayLibraryTests : IDisposable
{
    private static readonly DateTime Monday = new(2026, 10, 12, 18, 0, 0, DateTimeKind.Utc);
    private readonly TempLibrary library = new();

    public void Dispose() => library.Dispose();

    [Fact]
    public void ListsReplaysOfAllFoldersNewestFirst()
    {
        library.WriteReplay("own/EUW1-1.rofl", Monday, ("Coach", "Azir"));
        library.WriteReplay("players/EUW1-2.rofl", Monday.AddDays(1), ("Mid", "Ahri"));

        var entries = Scan();

        Assert.Equal(["EUW1-2.rofl", "EUW1-1.rofl"], entries.Select(e => e.FileName));
        Assert.Equal([ReplaySource.Players, ReplaySource.Own], entries.Select(e => e.Source));
        Assert.All(entries, e => Assert.True(e.Metadata.IsSuccess));
    }

    [Fact]
    public void NamesTheSubfolderAReplayIsIn()
    {
        library.WriteReplay("players/Northwind/scrim 1.rofl", Monday, ("Northwind", "Jhin"));
        library.WriteReplay("players/top.rofl", Monday, ("Enigma", "Sion"));

        var groups = Scan().ToDictionary(e => e.FileName, e => e.Group);

        Assert.Equal("Northwind", groups["scrim 1.rofl"]);
        Assert.Equal(string.Empty, groups["top.rofl"]);
    }

    [Fact]
    public void KeepsABrokenFileAsAnEntryWithItsError()
    {
        library.WriteFile("players/broken.rofl", [1, 2, 3], Monday);

        var entry = Assert.Single(Scan());

        Assert.False(entry.Metadata.TryGetValue(out _, out var error));
        Assert.Equal(RoflErrorKind.NotAReplay, error.Kind);
    }

    [Fact]
    public void SkipsFoldersThatDoNotExist()
    {
        Assert.Empty(ReplayLibrary.Scan([new LibraryFolder(library.Path("missing"), ReplaySource.Own)], CancellationToken.None));
    }

    [Fact]
    public void ListsAFileOnceWhenFoldersOverlap()
    {
        library.WriteReplay("players/EUW1-2.rofl", Monday, ("Mid", "Ahri"));

        var entries = ReplayLibrary.Scan(
            [new LibraryFolder(library.Path("players"), ReplaySource.Players), new LibraryFolder(library.Root, ReplaySource.Own)],
            CancellationToken.None);

        Assert.Single(entries);
    }

    [Fact]
    public void ListsACopyMadeForTheLeagueClientOnlyAsThePlayerReplay()
    {
        var original = library.WriteReplay("players/scrim 1.rofl", Monday, ("Mid", "Ahri"));
        Directory.CreateDirectory(library.Path("own"));
        File.Copy(original, library.Path("own", "EUW1-912345678901.rofl"));

        var entry = Assert.Single(Scan());

        Assert.Equal(ReplaySource.Players, entry.Source);
    }

    [Fact]
    public void StopsWhenCancelled()
    {
        library.WriteReplay("own/EUW1-1.rofl", Monday, ("Coach", "Azir"));

        Assert.Throws<OperationCanceledException>(() =>
            ReplayLibrary.Scan([new LibraryFolder(library.Path("own"), ReplaySource.Own)], new CancellationToken(canceled: true)));
    }

    private IReadOnlyList<ReplayEntry> Scan() => ReplayLibrary.Scan(
        [new LibraryFolder(library.Path("own"), ReplaySource.Own), new LibraryFolder(library.Path("players"), ReplaySource.Players)],
        CancellationToken.None);
}
