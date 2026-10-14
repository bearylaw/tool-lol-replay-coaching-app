namespace RiftReview.Library.Tests;

public sealed class ReplayImporterTests : IDisposable
{
    private static readonly DateTime Monday = new(2026, 10, 12, 18, 0, 0, DateTimeKind.Utc);
    private readonly TempLibrary library = new();

    public void Dispose() => library.Dispose();

    [Fact]
    public void CopiesAReplayIntoTheFolderAndKeepsTheOriginal()
    {
        var source = library.WriteReplay("downloads/EUW1-5.rofl", Monday, ("Mid", "Ahri"));

        var outcome = Assert.Single(ReplayImporter.Import([source], library.Path("players")));

        Assert.Equal(ImportResult.Imported, outcome.Result);
        Assert.Equal(library.Path("players", "EUW1-5.rofl"), outcome.TargetPath);
        Assert.True(File.Exists(source));
        Assert.Equal(File.ReadAllBytes(source), File.ReadAllBytes(outcome.TargetPath!));
    }

    [Fact]
    public void DoesNotCopyTheSameReplayTwice()
    {
        var source = library.WriteReplay("downloads/EUW1-5.rofl", Monday, ("Mid", "Ahri"));
        ReplayImporter.Import([source], library.Path("players"));

        var outcome = Assert.Single(ReplayImporter.Import([source], library.Path("players")));

        Assert.Equal(ImportResult.AlreadyPresent, outcome.Result);
        Assert.Single(Directory.GetFiles(library.Path("players")));
    }

    [Fact]
    public void RenamesWhenADifferentReplayHasTheSameName()
    {
        library.WriteReplay("players/game.rofl", Monday, ("Top", "Sion"));
        var source = library.WriteReplay("downloads/game.rofl", Monday, ("Mid", "Ahri"));

        var outcome = Assert.Single(ReplayImporter.Import([source], library.Path("players")));

        Assert.Equal(ImportResult.ImportedRenamed, outcome.Result);
        Assert.Equal(library.Path("players", "game (2).rofl"), outcome.TargetPath);
    }

    [Fact]
    public void RefusesFilesThatAreNotReplays()
    {
        var source = library.WriteFile("downloads/notes.rofl", "hello"u8.ToArray(), Monday);

        var outcome = Assert.Single(ReplayImporter.Import([source], library.Path("players")));

        Assert.Equal(ImportResult.NotAReplay, outcome.Result);
        Assert.Empty(Directory.GetFiles(library.Path("players")));
    }

    [Fact]
    public void LeavesAFileAlreadyInTheFolderAlone()
    {
        var source = library.WriteReplay("players/sub/EUW1-5.rofl", Monday, ("Mid", "Ahri"));

        var outcome = Assert.Single(ReplayImporter.Import([source], library.Path("players")));

        Assert.Equal(ImportResult.AlreadyPresent, outcome.Result);
        Assert.Equal(source, outcome.TargetPath);
    }

    [Fact]
    public void ReportsAMissingSourceAsFailed()
    {
        var outcome = Assert.Single(ReplayImporter.Import([library.Path("nope.rofl")], library.Path("players")));

        Assert.Equal(ImportResult.Failed, outcome.Result);
        Assert.NotNull(outcome.Detail);
    }
}
