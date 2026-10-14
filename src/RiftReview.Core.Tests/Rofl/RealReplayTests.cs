using RiftReview.Core.Rofl;

namespace RiftReview.Core.Tests.Rofl;

/// <summary>
/// Reads real replays from the folder in RIFTREVIEW_REPLAY_DIR. Skipped in the default run, since
/// real replays hold player data and stay out of the repository; run it after a new patch:
/// <c>just verify-replays "...\Replays"</c>.
/// </summary>
public sealed class RealReplayTests
{
    [Fact]
    public void ReadsEveryReplayInTheConfiguredFolder()
    {
        var folder = Environment.GetEnvironmentVariable("RIFTREVIEW_REPLAY_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder), "RIFTREVIEW_REPLAY_DIR is not set");

        var files = Directory.GetFiles(folder!, "*.rofl");
        Assert.NotEmpty(files);
        foreach (var path in files)
        {
            using var stream = File.OpenRead(path);
            var result = RoflReader.Read(stream);

            Assert.True(result.TryGetValue(out var metadata, out var error), $"{path}: {error?.Detail}");
            Assert.True(metadata.GameLength > TimeSpan.Zero, path);
            Assert.All(metadata.Players, p => Assert.False(string.IsNullOrEmpty(p.Champion)));
            TestContext.Current.SendDiagnosticMessage(
                $"{Path.GetFileName(path)}: {metadata.Version}, {metadata.GameLength}, {metadata.Players.Count} players, winner {metadata.WinningTeam}");
        }
    }
}
