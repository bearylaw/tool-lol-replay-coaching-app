using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using RiftReview.Core.Matches;

namespace RiftReview.Launch;

/// <summary>What the client reports about itself before a replay can start.</summary>
public sealed record ClientStatus(bool IsLoggedIn, bool IsPatching, bool IsPlayingGame, bool IsReplaysEnabled);

/// <summary>
/// The League client's local replay API on <c>https://127.0.0.1:{port}</c>, the calls its own Watch
/// button makes. Endpoints and payloads were read from the client's <c>/help</c> description
/// (patch 16.20). The client uses a self-signed certificate, so any certificate is accepted, but
/// only for the loopback address.
/// </summary>
public sealed class LeagueClientApi : IDisposable
{
    private readonly HttpClient http;

    /// <summary>Connects to the client described by <paramref name="lockfile"/>.</summary>
    public LeagueClientApi(ClientLockfile lockfile)
        : this(lockfile, CreateLoopbackHandler())
    {
    }

    /// <summary>For tests: the same client over another handler.</summary>
    public LeagueClientApi(ClientLockfile lockfile, HttpMessageHandler handler)
    {
        ArgumentNullException.ThrowIfNull(lockfile);
        http = new HttpClient(handler)
        {
            BaseAddress = new Uri(string.Create(CultureInfo.InvariantCulture, $"https://127.0.0.1:{lockfile.Port}/")),
            Timeout = TimeSpan.FromSeconds(15),
        };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"riot:{lockfile.Password}")));
    }

    /// <summary><c>GET /lol-replays/v1/configuration</c>: login, patching and game state.</summary>
    public async Task<ClientStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        using var document = await GetJsonAsync("lol-replays/v1/configuration", cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;
        return new ClientStatus(
            Flag(root, "isLoggedIn"),
            Flag(root, "isPatching"),
            Flag(root, "isPlayingGame") || Flag(root, "isPlayingReplay"),
            Flag(root, "isReplaysEnabled"));
    }

    /// <summary><c>GET /lol-replays/v1/rofls/path</c>: the folder the client reads replays from.</summary>
    public async Task<string> GetReplayFolderAsync(CancellationToken cancellationToken)
    {
        using var document = await GetJsonAsync("lol-replays/v1/rofls/path", cancellationToken).ConfigureAwait(false);
        return document.RootElement.ValueKind == JsonValueKind.String && document.RootElement.GetString() is { Length: > 0 } folder
            ? folder
            : throw new HttpRequestException("lol-replays/v1/rofls/path returned no folder");
    }

    /// <summary><c>POST /lol-replays/v1/rofls/scan</c>: makes the client pick up new files in its folder.</summary>
    public async Task ScanAsync(CancellationToken cancellationToken)
    {
        using var response = await http.PostAsync("lol-replays/v1/rofls/scan", null, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// <c>GET /lol-replays/v1/metadata/{gameId}</c>: the client's state for a replay (<c>watch</c>,
    /// <c>checking</c>, <c>incompatible</c>, …), or <c>null</c> when it has no metadata for it yet.
    /// </summary>
    public async Task<string?> GetReplayStateAsync(long gameId, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(Path("lol-replays/v1/metadata", gameId), cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        return document.RootElement.TryGetProperty("state", out var state) && state.ValueKind == JsonValueKind.String
            ? state.GetString()!
            : throw new HttpRequestException($"lol-replays/v1/metadata/{gameId} has no state");
    }

    /// <summary>
    /// <c>POST /lol-replays/v2/metadata/{gameId}/create</c>: registers a replay the client has no
    /// metadata for. Only the version is known from the file; type and queue are the values tested on 16.20.
    /// </summary>
    public async Task CreateMetadataAsync(long gameId, GameVersion version, DateTimeOffset gameEnd, CancellationToken cancellationToken)
    {
        var body = new
        {
            gameVersion = version.ToString(),
            gameType = "MATCHED_GAME",
            queueId = 420,
            gameEnd = gameEnd.ToUnixTimeMilliseconds(),
        };
        using var response = await http.PostAsJsonAsync(Path("lol-replays/v2/metadata", gameId) + "/create", body, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    /// <summary><c>POST /lol-replays/v1/rofls/{gameId}/watch</c>: starts the replay in the game.</summary>
    public async Task WatchAsync(long gameId, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(
            Path("lol-replays/v1/rofls", gameId) + "/watch",
            new { componentType = "replay-button_match-history" },
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc />
    public void Dispose() => http.Dispose();

    private async Task<JsonDocument> GetJsonAsync(string path, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(path, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
    }

    private static string Path(string prefix, long gameId) =>
        string.Create(CultureInfo.InvariantCulture, $"{prefix}/{gameId}");

    private static bool Flag(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static HttpClientHandler CreateLoopbackHandler() => new()
    {
        ServerCertificateCustomValidationCallback = (request, _, _, _) =>
            request.RequestUri is { Host: "127.0.0.1", Scheme: "https" },
    };
}
