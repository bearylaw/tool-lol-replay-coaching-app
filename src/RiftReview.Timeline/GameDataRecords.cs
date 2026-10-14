using System.Buffers.Binary;
using RiftReview.Core.Results;
using RiftReview.Core.Rofl;
using ZstdSharp;

namespace RiftReview.Timeline;

/// <summary>What a record in the game data holds (the stream tag at byte 8 of its header).</summary>
public enum RecordKind : byte
{
    /// <summary>Game chunk: the packets of a stretch of game time.</summary>
    Chunk = 1,

    /// <summary>Keyframe: a full state snapshot.</summary>
    Keyframe = 2,

    /// <summary>The initial keyframe, first record of the file.</summary>
    InitialKeyframe = 3,

    /// <summary>The start marker, one 17-byte block.</summary>
    StartMarker = 4,
}

/// <summary>One decompressed record of a replay's game data.</summary>
public sealed record GameDataRecord(RecordKind Kind, uint Id, byte[] Data);

/// <summary>
/// Reads the records between the header and the trailer of a ROFL2 file: a 17-byte header
/// (id, counter, zero bytes, kind, uncompressed size, compressed size) and a zstd frame, or raw
/// bytes when the compressed size is 0. Layout in docs/research/rofl2-payload.md.
/// </summary>
public static class GameDataRecords
{
    private const int HeaderLength = 17;

    // The largest record seen is under 1 MB uncompressed; the cap stops a corrupt size from
    // allocating gigabytes.
    private const int MaxRecordLength = 64 * 1024 * 1024;

    /// <summary>Reads and decompresses every record, or explains where the data is malformed.</summary>
    public static Result<IReadOnlyList<GameDataRecord>, string> ReadAll(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var range = RoflReader.GetGameDataRange(stream);
        if (!range.TryGetValue(out var bounds, out var error))
        {
            return Result<IReadOnlyList<GameDataRecord>, string>.Failure(error.Detail);
        }

        var records = new List<GameDataRecord>();
        var header = new byte[HeaderLength];
        using var decompressor = new Decompressor();
        stream.Position = bounds.Start;
        while (stream.Position < bounds.End)
        {
            var at = stream.Position;
            if (bounds.End - at < HeaderLength)
            {
                return Fail($"record header at {at} runs past the game data end {bounds.End}");
            }

            stream.ReadExactly(header);
            var id = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0, 4));
            var kind = (RecordKind)header[8];
            var uncompressed = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(9, 4));
            var compressed = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(13, 4));
            var stored = compressed > 0 ? compressed : uncompressed;
            if (uncompressed > MaxRecordLength || stored > bounds.End - stream.Position)
            {
                return Fail($"record at {at} declares {uncompressed} bytes ({stored} stored), past the game data end");
            }

            var raw = new byte[stored];
            stream.ReadExactly(raw);
            var data = compressed > 0 ? decompressor.Unwrap(raw).ToArray() : raw;
            if (data.Length != uncompressed)
            {
                return Fail($"record at {at} decompressed to {data.Length} bytes, header says {uncompressed}");
            }

            records.Add(new GameDataRecord(kind, id, data));
        }

        return Result<IReadOnlyList<GameDataRecord>, string>.Success(records);
    }

    private static Result<IReadOnlyList<GameDataRecord>, string> Fail(string detail) =>
        Result<IReadOnlyList<GameDataRecord>, string>.Failure(detail);
}
