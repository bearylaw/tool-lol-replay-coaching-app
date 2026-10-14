namespace RiftReview.Library.Tests;

public sealed class DefaultFoldersTests : IDisposable
{
    private readonly TempLibrary library = new();

    public void Dispose() => library.Dispose();

    [Fact]
    public void UsesTheRepositoryReplaysFolderWhenRunFromTheRepository()
    {
        library.WriteFile("repo/RiftReview.slnx", [], DateTime.UtcNow);
        library.WriteFile("repo/replays/README.md", [], DateTime.UtcNow);
        Directory.CreateDirectory(library.Path("repo", "dist"));

        Assert.Equal(library.Path("repo", "replays"), DefaultFolders.PlayerReplays(library.Path("repo", "dist"), library.Path("docs")));
    }

    [Fact]
    public void UsesDocumentsElsewhere()
    {
        Directory.CreateDirectory(library.Path("desktop"));

        Assert.Equal(
            library.Path("docs", "Rift Review", "Player Replays"),
            DefaultFolders.PlayerReplays(library.Path("desktop"), library.Path("docs")));
    }

    [Fact]
    public void PointsOwnReplaysAtTheLeagueClientFolder()
    {
        Assert.Equal(library.Path("docs", "League of Legends", "Replays"), DefaultFolders.OwnReplays(library.Path("docs")));
    }
}
