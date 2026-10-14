using System.Net;
using RiftReview.Core.Logging;
using RiftReview.Core.Matches;

namespace RiftReview.GameData.Tests;

public sealed class GameDataServiceTests : IDisposable
{
    private readonly string cache = Path.Combine(Path.GetTempPath(), "riftreview-tests", Guid.NewGuid().ToString("N"));
    private readonly FakeCdn cdn = new();
    private readonly StringWriter logOutput = new();

    public void Dispose()
    {
        if (Directory.Exists(cache))
        {
            Directory.Delete(cache, recursive: true);
        }
    }

    [Fact]
    public async Task DownloadsAnIconOnceAndThenServesItFromDisk()
    {
        cdn.Files["cdn/16.20.1/img/item/3109.png"] = [1, 2, 3];

        var first = await Service().GetIconFileAsync("cdn/16.20.1/img/item/3109.png");
        var again = await Service().GetIconFileAsync("cdn/16.20.1/img/item/3109.png");

        Assert.Equal([1, 2, 3], File.ReadAllBytes(first!));
        Assert.Equal(first, again);
        Assert.Equal(1, cdn.Requests.Count(r => r.EndsWith("3109.png", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task ReturnsNullAndLogsWhenTheCdnFails()
    {
        var icon = await Service().GetIconFileAsync("cdn/16.20.1/img/item/1.png");

        Assert.Null(icon);
        Assert.Contains("ddragon.download_failed", logOutput.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildsTheCatalogForTheReplaysPatch()
    {
        cdn.Files["api/versions.json"] = """["16.21.1","16.20.1"]"""u8.ToArray();
        cdn.Files["cdn/16.20.1/data/en_US/champion.json"] = """{"data":{"Azir":{"id":"Azir","name":"Azir"}}}"""u8.ToArray();
        cdn.Files["cdn/16.20.1/data/en_US/item.json"] = """{"data":{}}"""u8.ToArray();
        cdn.Files["cdn/16.20.1/data/en_US/summoner.json"] = """{"data":{}}"""u8.ToArray();
        cdn.Files["cdn/16.20.1/data/en_US/runesReforged.json"] = "[]"u8.ToArray();

        var catalog = await Service().GetCatalogAsync(new GameVersion(16, 20, 824, 8524));

        Assert.Equal("16.20.1", catalog?.Version);
        Assert.Equal("Azir", catalog?.FindChampion("azir")?.Name);
    }

    [Fact]
    public async Task UsesTheCachedVersionListWhenOffline()
    {
        cdn.Files["api/versions.json"] = """["16.20.1"]"""u8.ToArray();
        await Service().GetCatalogAsync(new GameVersion(16, 20, 0, 0));
        cdn.Files.Clear();

        var offline = Service();
        await offline.GetCatalogAsync(new GameVersion(16, 20, 0, 0));

        Assert.True(File.Exists(Path.Combine(cache, "api", "versions.json")));
    }

    [Fact]
    public async Task RefusesAPathThatLeavesTheCache()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Service().GetIconFileAsync("../../outside.png"));
    }

    private GameDataService Service() =>
        new(new HttpClient(cdn), cache, new AppLog(logOutput, TimeProvider.System));

    private sealed class FakeCdn : HttpMessageHandler
    {
        public Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);

        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath.TrimStart('/');
            Requests.Add(path);
            Assert.Equal("ddragon.leagueoflegends.com", request.RequestUri.Host);
            Assert.Equal(Uri.UriSchemeHttps, request.RequestUri.Scheme);
            return Task.FromResult(Files.TryGetValue(path, out var bytes)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
