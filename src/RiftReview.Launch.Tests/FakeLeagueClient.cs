using System.Net;
using System.Text;

namespace RiftReview.Launch.Tests;

/// <summary>
/// Answers like the League client's replay API (shapes copied from the 16.20 client) and records
/// every call. The replay state sequence is scripted per test.
/// </summary>
internal sealed class FakeLeagueClient(string replayFolder) : HttpMessageHandler
{
    public Queue<string?> States { get; } = new();

    public string Configuration { get; set; } =
        """{"gameVersion":"16.20.824.8524","isLoggedIn":true,"isPatching":false,"isPlayingGame":false,"isPlayingReplay":false,"isReplaysEnabled":true}""";

    public List<string> Calls { get; } = [];

    public string? LastAuthorization { get; private set; }

    public string? LastBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var call = $"{request.Method} {request.RequestUri!.AbsolutePath}";
        Calls.Add(call);
        LastAuthorization = request.Headers.Authorization?.ToString();
        if (request.Content is not null)
        {
            LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
        }

        return call switch
        {
            "GET /lol-replays/v1/configuration" => Json(Configuration),
            "GET /lol-replays/v1/rofls/path" => Json($"\"{replayFolder.Replace('\\', '/')}\""),
            _ when call.StartsWith("GET /lol-replays/v1/metadata/", StringComparison.Ordinal) => State(),
            _ when request.Method == HttpMethod.Post => new HttpResponseMessage(HttpStatusCode.NoContent),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        };
    }

    private HttpResponseMessage State()
    {
        var state = States.Count > 1 ? States.Dequeue() : States.Peek();
        return state is null
            ? new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("""{"message":"Plugin found no local metadata."}""") }
            : Json($$"""{"downloadProgress":0,"gameId":1,"state":"{{state}}"}""");
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}
