namespace RiftReview.Timeline.Tests;

public sealed class GameDataRecordsTests
{
    [Fact]
    public void ReadsCompressedAndRawRecordsInOrder()
    {
        using var file = new SyntheticReplay()
            .WithRecord(RecordKind.InitialKeyframe, 1, [1, 2, 3, 4])
            .WithRecord(RecordKind.StartMarker, 2, [5, 6], compress: false)
            .WithRecord(RecordKind.Chunk, 3, Enumerable.Repeat((byte)7, 5000).ToArray())
            .Build();

        var result = GameDataRecords.ReadAll(file);

        Assert.True(result.TryGetValue(out var records, out var error), error);
        Assert.Equal([RecordKind.InitialKeyframe, RecordKind.StartMarker, RecordKind.Chunk], records.Select(r => r.Kind));
        Assert.Equal([1u, 2u, 3u], records.Select(r => r.Id));
        Assert.Equal([5, 6], records[1].Data);
        Assert.Equal(5000, records[2].Data.Length);
    }

    [Fact]
    public void RejectsARecordLargerThanTheGameData()
    {
        using var file = new SyntheticReplay()
            .WithRecord(RecordKind.Chunk, 3, [1, 2, 3])
            .WithOversizedRecord(0)
            .Build();

        var result = GameDataRecords.ReadAll(file);

        Assert.False(result.TryGetValue(out _, out var error));
        Assert.Contains("past the game data end", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadsTheLongerRecordHeadersOfNewerPatches()
    {
        using var file = new SyntheticReplay()
            .WithRecord(RecordKind.InitialKeyframe, 1, [1, 2, 3, 4])
            .WithRecord(RecordKind.StartMarker, 2, [5, 6], compress: false)
            .WithRecord(RecordKind.Chunk, 3, Enumerable.Repeat((byte)7, 5000).ToArray())
            .WithLongHeaders()
            .Build();

        Assert.True(GameDataRecords.ReadAll(file).TryGetValue(out var records, out var error), error);
        Assert.Equal([RecordKind.InitialKeyframe, RecordKind.StartMarker, RecordKind.Chunk], records.Select(r => r.Kind));
        Assert.Equal([1, 2, 3, 4], records[0].Data);
        Assert.Equal([5, 6], records[1].Data);
        Assert.Equal(5000, records[2].Data.Length);
    }

    [Fact]
    public void ARecordThatDoesNotDecompressIsAFailureNotACrash()
    {
        using var file = new SyntheticReplay()
            .WithRecord(RecordKind.InitialKeyframe, 1, Enumerable.Repeat((byte)1, 100).ToArray())
            .WithUndecodableRecord(0)
            .Build();

        Assert.False(GameDataRecords.ReadAll(file).TryGetValue(out _, out var error));
        Assert.Contains("does not decompress", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadsAFileWithoutRecords()
    {
        using var file = new SyntheticReplay().Build();

        Assert.True(GameDataRecords.ReadAll(file).TryGetValue(out var records, out _));
        Assert.Empty(records);
    }
}
