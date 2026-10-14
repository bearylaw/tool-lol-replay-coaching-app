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
        Assert.Equal(new CameraView(4520.07, 1911.85, 4249.95, 0, 56, 0, 40, "top"), state.Camera);
    }

    [Theory]
    [InlineData("""{"selectionName":""}""")]
    [InlineData("""{ nope""")]
    public void ExplainsAnUnreadableRender(string json)
    {
        Assert.False(RenderStateParser.Parse(json).TryGetValue(out _, out var error));
        Assert.NotEmpty(error);
    }

    // Checked on a running 16.20 replay: the free camera at z = 3710.44 + 1911.85 / tan(56°), then
    // the top camera, gives back the top camera at z = 3710.44.
    [Fact]
    public async Task RestoresTheTopCameraThroughTheFreeCamera()
    {
        var handler = new Recorder();
        using var game = new GameApi(handler);

        await new ReplayRemote(game).SetCameraAsync(new CameraView(7000, 1911.848, 3710.442, 0, 56, 0, 40, "top"), CancellationToken.None);

        Assert.Equal(2, handler.Bodies.Count);
        using var free = JsonDocument.Parse(handler.Bodies[0]);
        Assert.Equal("fps", free.RootElement.GetProperty("cameraMode").GetString());
        Assert.Equal(5000, free.RootElement.GetProperty("cameraPosition").GetProperty("z").GetDouble(), 0);
        using var back = JsonDocument.Parse(handler.Bodies[1]);
        Assert.Equal("top", back.RootElement.GetProperty("cameraMode").GetString());
    }

    [Fact]
    public async Task LeavesAFreeCameraFree()
    {
        var handler = new Recorder();
        using var game = new GameApi(handler);

        await new ReplayRemote(game).SetCameraAsync(new CameraView(7000, 1911.85, 7000, 227, 3, 0, 40, "fps"), CancellationToken.None);

        using var body = JsonDocument.Parse(Assert.Single(handler.Bodies));
        Assert.Equal(7000, body.RootElement.GetProperty("cameraPosition").GetProperty("z").GetDouble());
        Assert.Equal("fps", body.RootElement.GetProperty("cameraMode").GetString());
    }

    private sealed class Recorder : HttpMessageHandler
    {
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("/replay/render", request.RequestUri!.AbsolutePath);
            Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
        }
    }
}
