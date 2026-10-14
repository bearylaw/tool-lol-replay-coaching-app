using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace RiftReview.Library.Tests;

/// <summary>A temporary folder tree with synthetic ROFL2 files, deleted after the test.</summary>
internal sealed class TempLibrary : IDisposable
{
    public string Root { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "riftreview-tests", Guid.NewGuid().ToString("N"));

    public TempLibrary() => Directory.CreateDirectory(Root);

    public void Dispose() => Directory.Delete(Root, recursive: true);

    public string Path(params string[] parts) => System.IO.Path.Combine([Root, .. parts]);

    /// <summary>Writes a replay whose players are (Riot name, champion) pairs; the first team listed wins.</summary>
    public string WriteReplay(string relativePath, DateTime modifiedUtc, params (string Name, string Champion)[] players)
    {
        var stats = players.Select((p, i) => new Dictionary<string, string>
        {
            ["RIOT_ID_GAME_NAME"] = p.Name,
            ["RIOT_ID_TAG_LINE"] = "EUW",
            ["SKIN"] = p.Champion,
            ["TEAM"] = i < 5 ? "100" : "200",
            ["WIN"] = i < 5 ? "Win" : "Fail",
        });
        var metadata = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["gameLength"] = 1_500_000,
            ["statsJson"] = JsonSerializer.Serialize(stats),
        }));

        using var stream = new MemoryStream();
        stream.Write([.. "RIOT"u8, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0]);
        stream.WriteByte(14);
        stream.Write("16.20.824.8524"u8);
        stream.Write(new byte[64]);
        stream.Write(metadata);
        Span<byte> footer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(footer, metadata.Length);
        stream.Write(footer);

        return WriteFile(relativePath, stream.ToArray(), modifiedUtc);
    }

    public string WriteFile(string relativePath, byte[] content, DateTime modifiedUtc)
    {
        var path = System.IO.Path.GetFullPath(Path(relativePath));
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
        File.SetLastWriteTimeUtc(path, modifiedUtc);
        return path;
    }
}
