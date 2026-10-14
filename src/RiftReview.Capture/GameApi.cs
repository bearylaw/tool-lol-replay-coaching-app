using System.Net.Http.Json;
using System.Text.Json;

namespace RiftReview.Capture;

/// <summary>Replay playback state from <c>/replay/playback</c>.</summary>
public sealed record Playback(double Time, double Length, bool Paused, double Speed);

/// <summary>
/// The running game's local API on <c>https://127.0.0.1:2999</c>: Riot's documented replay API
/// (needs <c>EnableReplayApi=1</c> in game.cfg) and Live Client Data API. The game uses a
/// self-signed certificate; it is accepted only for the loopback address.
/// </summary>
public sealed class GameApi : IDisposable
{
    private readonly HttpClient http;

    /// <summary>Connects to the game on this machine.</summary>
    public GameApi()
        : this(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (request, _, _, _) => request.RequestUri is { Host: "127.0.0.1", Scheme: "https" },
        })
    {
    }

    /// <summary>For tests: the same client over another handler.</summary>
    public GameApi(HttpMessageHandler handler)
    {
        http = new HttpClient(handler) { BaseAddress = new Uri("https://127.0.0.1:2999/"), Timeout = TimeSpan.FromSeconds(5) };
    }

    /// <summary>The playback state, or <c>null</c> while no replay with the replay API is running.</summary>
    public async Task<Playback?> GetPlaybackAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.GetAsync("replay/playback", cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            var root = document.RootElement;
            return new Playback(
                root.GetProperty("time").GetDouble(),
                root.GetProperty("length").GetDouble(),
                root.GetProperty("paused").GetBoolean(),
                root.GetProperty("speed").GetDouble());
        }
        catch (HttpRequestException)
        {
            // The game is not running yet or is loading: no listener on the port.
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    /// <summary>Changes playback; <c>null</c> values are left as they are.</summary>
    public async Task SetPlaybackAsync(double? time, bool? paused, double? speed, CancellationToken cancellationToken)
    {
        var body = new Dictionary<string, object>();
        if (time is { } t)
        {
            body["time"] = t;
        }

        if (paused is { } p)
        {
            body["paused"] = p;
        }

        if (speed is { } s)
        {
            body["speed"] = s;
        }

        using var response = await http.PostAsJsonAsync("replay/playback", body, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Changes render settings, for example <c>fogOfWar</c>; see Riot's replay API.</summary>
    public async Task SetRenderAsync(IReadOnlyDictionary<string, object> settings, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync("replay/render", settings, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>The raw <c>allgamedata</c> JSON.</summary>
    public Task<string> GetAllGameDataAsync(CancellationToken cancellationToken) =>
        http.GetStringAsync("liveclientdata/allgamedata", cancellationToken);

    /// <summary>The raw <c>eventdata</c> JSON.</summary>
    public Task<string> GetEventsAsync(CancellationToken cancellationToken) =>
        http.GetStringAsync("liveclientdata/eventdata", cancellationToken);

    /// <inheritdoc />
    public void Dispose() => http.Dispose();
}
