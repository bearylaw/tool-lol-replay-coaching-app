using System.Text.Json;
using RiftReview.Core.Logging;

namespace RiftReview.Core.Tests.Logging;

public sealed class AppLogTests
{
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public void WritesOneJsonObjectPerLineWithTimeLevelAndFields()
    {
        var output = new StringWriter();
        using var log = new AppLog(output, new FixedClock(new DateTimeOffset(2026, 10, 14, 8, 30, 0, TimeSpan.Zero)));

        log.Info("scan.finished", ("replays", 6), ("ms", 120));

        var line = JsonDocument.Parse(output.ToString().Trim()).RootElement;
        Assert.Equal("2026-10-14T08:30:00.0000000+00:00", line.GetProperty("ts").GetString());
        Assert.Equal("info", line.GetProperty("level").GetString());
        Assert.Equal("scan.finished", line.GetProperty("event").GetString());
        Assert.Equal("6", line.GetProperty("replays").GetString());
    }

    [Fact]
    public void KeepsTheOriginalExceptionOfAnError()
    {
        var output = new StringWriter();
        using var log = new AppLog(output, TimeProvider.System);

        log.Error("launch.failed", new InvalidOperationException("boom"));

        var line = JsonDocument.Parse(output.ToString().Trim()).RootElement;
        Assert.Equal("error", line.GetProperty("level").GetString());
        Assert.Contains("InvalidOperationException: boom", line.GetProperty("error").GetString(), StringComparison.Ordinal);
    }
}
