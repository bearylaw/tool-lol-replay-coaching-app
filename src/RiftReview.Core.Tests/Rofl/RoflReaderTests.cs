using RiftReview.Core.Matches;
using RiftReview.Core.Rofl;
using static RiftReview.Core.Tests.Rofl.RoflFileBuilder;

namespace RiftReview.Core.Tests.Rofl;

public sealed class RoflReaderTests
{
    [Fact]
    public void ReadsVersionLengthAndPlayers()
    {
        using var file = AReplay()
            .WithGameLengthMs(1_506_188)
            .WithPlayer(APlayer("Blue Mid", "Azir", 100, won: true))
            .WithPlayer(APlayer("Red Mid", "Leblanc", 200, won: false))
            .BuildStream();

        var metadata = ReadSuccessfully(file);

        Assert.Equal(new GameVersion(16, 20, 824, 8524), metadata.Version);
        Assert.Equal(TimeSpan.FromMilliseconds(1_506_188), metadata.GameLength);
        Assert.Equal(["Azir", "Leblanc"], metadata.Players.Select(p => p.Champion));
        Assert.Equal(Team.Blue, metadata.WinningTeam);
    }

    [Fact]
    public void ReadsVersionsOfOtherLengths()
    {
        using var file = AReplay().WithVersion("16.9.812.123").BuildStream();

        Assert.Equal(new GameVersion(16, 9, 812, 123), ReadSuccessfully(file).Version);
    }

    [Fact]
    public void AcceptsAReplayWithoutPlayerStats()
    {
        // A short private lobby game writes "statsJson":"[]".
        using var file = AReplay().WithGameLengthMs(71_204).BuildStream();

        var metadata = ReadSuccessfully(file);

        Assert.Empty(metadata.Players);
        Assert.Equal(Team.Other, metadata.WinningTeam);
    }

    [Fact]
    public void RejectsAFileWithoutTheRiotSignature()
    {
        using var file = AReplay().WithSignature([.. "PK"u8, 3, 4, 0, 0]).BuildStream();

        Assert.Equal(RoflErrorKind.NotAReplay, ReadError(file).Kind);
    }

    [Fact]
    public void RejectsTheOlderContainerFormat()
    {
        using var file = AReplay().WithSignature([.. "RIOT"u8, 0, 0]).BuildStream();

        Assert.Equal(RoflErrorKind.UnsupportedFormat, ReadError(file).Kind);
    }

    [Fact]
    public void RejectsAnEmptyFile()
    {
        using var file = new MemoryStream([]);

        Assert.Equal(RoflErrorKind.NotAReplay, ReadError(file).Kind);
    }

    [Fact]
    public void RejectsAMetadataLengthLargerThanTheFile()
    {
        using var file = AReplay().WithFooterLength(1_000_000).BuildStream();

        Assert.Equal(RoflErrorKind.Truncated, ReadError(file).Kind);
    }

    [Fact]
    public void RejectsAZeroMetadataLength()
    {
        using var file = AReplay().WithFooterLength(0).BuildStream();

        Assert.Equal(RoflErrorKind.Truncated, ReadError(file).Kind);
    }

    [Fact]
    public void RejectsAHeaderWithoutAVersion()
    {
        using var file = AReplay().WithVersion("garbage").BuildStream();

        Assert.Equal(RoflErrorKind.InvalidMetadata, ReadError(file).Kind);
    }

    [Theory]
    [InlineData("""{"gameLength":1000}""", "statsJson")]
    [InlineData("""{"statsJson":"[]"}""", "gameLength")]
    [InlineData("""{"gameLength":-5,"statsJson":"[]"}""", "gameLength")]
    [InlineData("""[1,2]""", "JSON object")]
    [InlineData("""{"gameLength":1000,"statsJson":"{}"}""", "array")]
    [InlineData("""{not json""", "valid JSON")]
    public void ExplainsWhatIsWrongWithTheMetadata(string json, string expectedDetail)
    {
        using var file = AReplay().WithMetadataJson(json).BuildStream();

        var error = ReadError(file);

        Assert.Equal(RoflErrorKind.InvalidMetadata, error.Kind);
        Assert.Contains(expectedDetail, error.Detail, StringComparison.Ordinal);
    }

    private static ReplayMetadata ReadSuccessfully(Stream file)
    {
        var result = RoflReader.Read(file);
        Assert.True(result.TryGetValue(out var metadata, out var error), error?.Detail);
        return metadata;
    }

    private static RoflError ReadError(Stream file)
    {
        var result = RoflReader.Read(file);
        Assert.False(result.TryGetValue(out _, out var error));
        return error;
    }
}
