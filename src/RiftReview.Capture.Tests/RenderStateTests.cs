using System.Net;
using System.Text.Json;

namespace RiftReview.Capture.Tests;

public sealed class RenderStateTests
{
    // Cut down from /replay/render on a running 16.20 replay with a player selected.
    private const string Render = """
        {"cameraAttached":false,"cameraMode":"top","cameraPosition":{"x":4520.07,"y":1911.85,"z":4249.95},
         "cameraRotation":{"x":0.0,"y":56.0,"z":0.0},"fieldOfView":40.0,"fogOfWar":true,
         "selectionName":"BearY","selectionOffset":{"x":0,"y":0,"z":0}}
        """;

    [Fact]
    public void ReadsTheSelectedPlayerAndTheCamera()
    {
        Assert.True(RenderStateParser.Parse(Render).TryGetValue(out var state, out var error), error);

        Assert.Equal("BearY", state.SelectionName);
        Assert.Equal(new CameraView(4520.07, 1911.85, 4249.95, 0, 56, 0, 40), state.Camera);
    }

    [Theory]
    [InlineData("""{"selectionName":""}""")]
    [InlineData("""{ nope""")]
    public void ExplainsAnUnreadableRender(string json)
    {
        Assert.False(RenderStateParser.Parse(json).TryGetValue(out _, out var error));
        Assert.NotEmpty(error);
    }

    // League keeps a set camera only in its free camera, so the camera mode goes with it.
    [Fact]
    public async Task PutsTheCameraInFreeModeWhereItWas()
    {
        var handler = new Recorder();
        using var game = new GameApi(handler);

        await new ReplayRemote(game).SetCameraAsync(new CameraView(7000, 1911.85, 7000, 0, 56, 0, 40), CancellationToken.None);

        using var body = JsonDocument.Parse(handler.Body!);
        var root = body.RootElement;
        Assert.Equal("replay/render", handler.Path);
        Assert.Equal("fps", root.GetProperty("cameraMode").GetString());
        Assert.False(root.GetProperty("cameraAttached").GetBoolean());
        Assert.Equal(7000, root.GetProperty("cameraPosition").GetProperty("z").GetDouble());
        Assert.Equal(56, root.GetProperty("cameraRotation").GetProperty("y").GetDouble());
    }

    private sealed class Recorder : HttpMessageHandler
    {
        public string? Path { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Path = request.RequestUri!.AbsolutePath.TrimStart('/');
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
        }
    }
}
