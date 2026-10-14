using RiftReview.Core.Matches;

namespace RiftReview.Launch.Tests;

public sealed class ClientReplayWatcherTests : IDisposable
{
    private static readonly GameVersion Patch = new(16, 20, 824, 8524);

    private readonly string root = Path.Combine(Path.GetTempPath(), "riftreview-tests", Guid.NewGuid().ToString("N"));
    private readonly FakeLeagueClient client;
    private readonly ClientReplayWatcher watcher = new(new InstantClock());

    public ClientReplayWatcherTests()
    {
        Directory.CreateDirectory(root);
        client = new FakeLeagueClient(ClientFolder);
    }

    private string ClientFolder => Path.Combine(root, "client");

    public void Dispose() => Directory.Delete(root, recursive: true);

    [Fact]
    public async Task CopiesAPlayerReplayIntoTheClientFolderAndWatchesIt()
    {
        var replay = WriteFile(Path.Combine("players", "EUW1-7000000003.rofl"), "replay");
        client.States.Enqueue("watch");

        var result = await Watch(replay);

        Assert.True(result.TryGetValue(out var played, out var failure), failure.Detail);
        Assert.Equal(Path.Combine(ClientFolder, "EUW1-7000000003.rofl"), played);
        Assert.Equal("replay", File.ReadAllText(played));
        Assert.Equal(
            ["GET /lol-replays/v1/configuration", "GET /lol-replays/v1/rofls/path", "POST /lol-replays/v1/rofls/scan",
             "GET /lol-replays/v1/metadata/7000000003", "POST /lol-replays/v1/rofls/7000000003/watch"],
            client.Calls);
        Assert.Equal("""{"componentType":"replay-button_match-history"}""", client.LastBody);
    }

    [Fact]
    public async Task AuthenticatesWithTheLockfilePassword()
    {
        client.States.Enqueue("watch");

        await Watch(WriteFile(Path.Combine("client", "EUW1-1.rofl"), "replay"));

        Assert.Equal("Basic " + Convert.ToBase64String("riot:secret"u8.ToArray()), client.LastAuthorization);
    }

    [Fact]
    public async Task RegistersTheReplayWhenTheScanDidNot()
    {
        var replay = WriteFile(Path.Combine("client", "EUW1-5.rofl"), "replay");
        client.States.Enqueue(null);
        client.States.Enqueue("checking");
        client.States.Enqueue("watch");

        var result = await Watch(replay);

        Assert.True(result.IsSuccess);
        Assert.Contains("POST /lol-replays/v2/metadata/5/create", client.Calls);
        Assert.Contains("POST /lol-replays/v1/rofls/5/watch", client.Calls);
    }

    [Fact]
    public async Task ReportsAReplayTheClientCannotPlay()
    {
        client.States.Enqueue("incompatible");

        var failure = await Failure(WriteFile("EUW1-6.rofl", "replay"));

        Assert.Equal(WatchFailure.Incompatible, failure);
        Assert.DoesNotContain(client.Calls, c => c.EndsWith("/watch", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("""{"isLoggedIn":false,"isPatching":false,"isPlayingGame":false,"isReplaysEnabled":true}""", WatchFailure.NotLoggedIn)]
    [InlineData("""{"isLoggedIn":true,"isPatching":true,"isPlayingGame":false,"isReplaysEnabled":true}""", WatchFailure.Patching)]
    [InlineData("""{"isLoggedIn":true,"isPatching":false,"isPlayingGame":true,"isReplaysEnabled":true}""", WatchFailure.GameRunning)]
    [InlineData("""{"isLoggedIn":true,"isPatching":false,"isPlayingGame":false,"isPlayingReplay":true,"isReplaysEnabled":true}""", WatchFailure.GameRunning)]
    [InlineData("""{"isLoggedIn":true,"isPatching":false,"isPlayingGame":false,"isReplaysEnabled":false}""", WatchFailure.ReplaysDisabled)]
    public async Task ChecksTheClientBeforeTouchingAnything(string configuration, WatchFailure expected)
    {
        client.Configuration = configuration;

        Assert.Equal(expected, await Failure(WriteFile(Path.Combine("players", "EUW1-7.rofl"), "replay")));
        Assert.False(Directory.Exists(ClientFolder));
    }

    [Fact]
    public async Task GivesUpWhenTheReplayNeverGetsReady()
    {
        client.States.Enqueue("checking");

        Assert.Equal(WatchFailure.NotReady, await Failure(WriteFile("EUW1-8.rofl", "replay")));
    }

    [Fact]
    public async Task RefusesToOverwriteADifferentFileInTheClientFolder()
    {
        WriteFile(Path.Combine("client", "EUW1-9.rofl"), "another game");

        Assert.Equal(WatchFailure.NameTaken, await Failure(WriteFile(Path.Combine("players", "EUW1-9.rofl"), "replay")));
        Assert.Equal("another game", File.ReadAllText(Path.Combine(ClientFolder, "EUW1-9.rofl")));
    }

    [Fact]
    public async Task ReusesAnIdenticalCopyAlreadyInTheClientFolder()
    {
        WriteFile(Path.Combine("client", "EUW1-10.rofl"), "replay");
        client.States.Enqueue("watch");

        var result = await Watch(WriteFile(Path.Combine("players", "EUW1-10.rofl"), "replay"));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void GivesARenamedFileAStableMadeUpGameId()
    {
        var replay = WriteFile("scrim game 2.rofl", "replay content");

        var name = ClientReplayWatcher.ClientNameFor(replay, "EUW");

        Assert.NotNull(name);
        Assert.Equal("EUW1", name.PlatformId);
        Assert.InRange(name.GameId, 900_000_000_000, 999_999_999_999);
        Assert.Equal(name, ClientReplayWatcher.ClientNameFor(replay, "EUW"));
        Assert.Null(ClientReplayWatcher.ClientNameFor(replay, "MARS"));
    }

    [Fact]
    public void KeepsTheClientNameOfAnUnrenamedFile()
    {
        var replay = WriteFile("KR-7123456789.rofl", "replay");

        Assert.Equal(new ReplayFileName("KR", 7123456789), ClientReplayWatcher.ClientNameFor(replay, "EUW"));
    }

    private string WriteFile(string relative, string content)
    {
        var path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private async Task<RiftReview.Core.Results.Result<string, (WatchFailure Reason, string Detail)>> Watch(string replay)
    {
        using var api = new LeagueClientApi(new ClientLockfile(55339, "secret"), client);
        return await watcher.WatchAsync(api, replay, Patch, "EUW", CancellationToken.None);
    }

    private async Task<WatchFailure> Failure(string replay)
    {
        var result = await Watch(replay);
        Assert.False(result.TryGetValue(out _, out var failure));
        return failure.Reason;
    }

    /// <summary>Makes Task.Delay return at once, so polling tests run in no time.</summary>
    private sealed class InstantClock : TimeProvider
    {
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            System.CreateTimer(callback, state, TimeSpan.Zero, Timeout.InfiniteTimeSpan);
    }
}
