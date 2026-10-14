using System.Collections.Concurrent;
using RiftReview.Core.Logging;
using RiftReview.Core.Matches;

namespace RiftReview.GameData;

/// <summary>
/// Downloads Data Dragon data and icons over HTTPS and caches them on disk, so each file is fetched
/// once and the app keeps its icons offline. Every failure is logged and returns <c>null</c>; the
/// UI then shows names instead of icons.
/// </summary>
public sealed class GameDataService
{
    private static readonly Uri BaseUri = new("https://ddragon.leagueoflegends.com/");

    private readonly HttpClient http;
    private readonly string cacheDirectory;
    private readonly AppLog log;
    private readonly ConcurrentDictionary<string, Lazy<Task<GameCatalog?>>> catalogs = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Lazy<Task<string?>>> files = new(StringComparer.Ordinal);
    private readonly Lazy<Task<IReadOnlyList<string>>> versions;

    /// <summary>Creates the service. <paramref name="http"/> should have a timeout; the service does not own it.</summary>
    public GameDataService(HttpClient http, string cacheDirectory, AppLog log)
    {
        this.http = http ?? throw new ArgumentNullException(nameof(http));
        this.cacheDirectory = cacheDirectory ?? throw new ArgumentNullException(nameof(cacheDirectory));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        versions = new Lazy<Task<IReadOnlyList<string>>>(LoadVersionsAsync);
    }

    /// <summary>The catalog matching a replay's patch, or <c>null</c> when Data Dragon cannot be reached and nothing is cached.</summary>
    public async Task<GameCatalog?> GetCatalogAsync(GameVersion replayVersion)
    {
        var version = DataDragonParser.PickVersion(await versions.Value.ConfigureAwait(false), replayVersion);
        if (version is null)
        {
            return null;
        }

        return await catalogs.GetOrAdd(version, v => new Lazy<Task<GameCatalog?>>(() => LoadCatalogAsync(v))).Value.ConfigureAwait(false);
    }


    /// <summary>
    /// The local file of an icon given its CDN path (from <see cref="GameCatalog"/> or
    /// <see cref="DataDragonParser"/>), downloading it on first use. <c>null</c> when unavailable.
    /// </summary>
    public Task<string?> GetIconFileAsync(string cdnPath) =>
        files.GetOrAdd(cdnPath, p => new Lazy<Task<string?>>(() => DownloadAsync(p, cacheFirst: true))).Value;

    private async Task<IReadOnlyList<string>> LoadVersionsAsync()
    {
        // versions.json changes every patch, so the network copy wins and the cached one is the fallback.
        var path = await DownloadAsync("api/versions.json", cacheFirst: false).ConfigureAwait(false);
        if (path is null)
        {
            return [];
        }

        try
        {
            return DataDragonParser.ParseVersions(await File.ReadAllTextAsync(path).ConfigureAwait(false));
        }
        catch (System.Text.Json.JsonException exception)
        {
            log.Warning("ddragon.versions_invalid", exception);
            return [];
        }
    }

    private async Task<GameCatalog?> LoadCatalogAsync(string version)
    {
        var names = new[] { "champion", "item", "summoner", "runesReforged" };
        var paths = await Task.WhenAll(names.Select(n => DownloadAsync($"cdn/{version}/data/en_US/{n}.json", cacheFirst: true))).ConfigureAwait(false);
        if (paths.Any(p => p is null))
        {
            return null;
        }

        try
        {
            var json = await Task.WhenAll(paths.Select(p => File.ReadAllTextAsync(p!))).ConfigureAwait(false);
            return DataDragonParser.ParseCatalog(version, json[0], json[1], json[2], json[3]);
        }
        catch (Exception exception) when (exception is System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException)
        {
            log.Warning("ddragon.catalog_invalid", exception, ("version", version));
            return null;
        }
    }

    private async Task<string?> DownloadAsync(string cdnPath, bool cacheFirst)
    {
        var localPath = Path.GetFullPath(Path.Combine(cacheDirectory, cdnPath));
        if (!localPath.StartsWith(Path.GetFullPath(cacheDirectory), StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Data Dragon path escapes the cache: {cdnPath}", nameof(cdnPath));
        }

        if (cacheFirst && File.Exists(localPath))
        {
            return localPath;
        }

        try
        {
            using var response = await http.GetAsync(new Uri(BaseUri, cdnPath)).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

            Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
            var temporary = $"{localPath}.{Guid.NewGuid():N}.part";
            await File.WriteAllBytesAsync(temporary, bytes).ConfigureAwait(false);
            File.Move(temporary, localPath, overwrite: true);
            return localPath;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException)
        {
            log.Warning("ddragon.download_failed", exception, ("path", cdnPath));
            return File.Exists(localPath) ? localPath : null;
        }
    }
}
