using System.Globalization;
using RiftReview.App.Replays;

namespace RiftReview.App.Tests;

public sealed class MatchFormatTests
{
    public MatchFormatTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
    }

    [Theory]
    [InlineData(1_506_188, "25:06")]
    [InlineData(71_204, "1:11")]
    [InlineData(3_727_000, "1:02:07")]
    public void WritesGameLengthLikeTheClient(long milliseconds, string expected)
    {
        Assert.Equal(expected, MatchFormat.Duration(TimeSpan.FromMilliseconds(milliseconds)));
    }

    [Theory]
    [InlineData(15, 2, 13, "14.00")]
    [InlineData(3, 3, 15, "6.00")]
    [InlineData(4, 0, 9, "Perfect")]
    public void ComputesTheKdaRatio(int kills, int deaths, int assists, string expected)
    {
        Assert.Equal(expected, MatchFormat.KdaRatio(kills, deaths, assists));
    }

    [Fact]
    public void ComputesPerMinuteRates()
    {
        Assert.Equal("8.0", MatchFormat.PerMinute(200, TimeSpan.FromMinutes(25)));
        Assert.Equal("0", MatchFormat.PerMinute(200, TimeSpan.Zero));
    }

    [Theory]
    [InlineData(16781, "16.8k")]
    [InlineData(999, "999")]
    public void ShortensThousands(long value, string expected)
    {
        Assert.Equal(expected, MatchFormat.Thousands(value));
    }
}
