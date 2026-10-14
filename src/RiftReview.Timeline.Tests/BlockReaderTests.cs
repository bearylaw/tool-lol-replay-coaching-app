namespace RiftReview.Timeline.Tests;

public sealed class BlockReaderTests
{
    [Fact]
    public void ReadsLongAndShortBlocksWithTheirDeltas()
    {
        var data = new Blocks()
            .Full(12.5f, 0x02d3, 0x400000ae, 1, 2, 3)
            .Short(250, 2, 9)
            .ToArray();

        var blocks = ReadAll(data, out var error);

        Assert.Null(error);
        Assert.Equal(2, blocks.Count);
        Assert.Equal(new Block(12.5f, 0x02d3, 0x400000ae, 15, 3), blocks[0]);
        Assert.Equal(12.75f, blocks[1].Time, 3);
        Assert.Equal(0x02d3, blocks[1].Type);
        Assert.Equal(0x400000b0u, blocks[1].Parameter);
        Assert.Equal([9], data.AsSpan(blocks[1].PayloadOffset, blocks[1].PayloadLength).ToArray());
    }

    [Fact]
    public void StopsWithAnErrorWhenAPayloadRunsPastTheEnd()
    {
        var data = new Blocks().Full(1f, 1, 0, 1, 2, 3, 4).ToArray()[..^2];

        var blocks = ReadAll(data, out var error);

        Assert.Empty(blocks);
        Assert.Contains("declares 4 payload bytes", error, StringComparison.Ordinal);
    }

    [Fact]
    public void StopsWithAnErrorWhenAHeaderIsCut()
    {
        ReadAll([0x00, 1, 2], out var error);

        Assert.Contains("runs past the end", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadsNothingFromNoData()
    {
        Assert.Empty(ReadAll([], out var error));
        Assert.Null(error);
    }

    private static List<Block> ReadAll(byte[] data, out string? error)
    {
        var reader = new BlockReader(data);
        var blocks = new List<Block>();
        while (reader.TryRead(out var block))
        {
            blocks.Add(block);
        }

        error = reader.Error;
        return blocks;
    }
}
