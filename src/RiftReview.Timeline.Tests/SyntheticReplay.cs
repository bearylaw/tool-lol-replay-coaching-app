using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using ZstdSharp;

namespace RiftReview.Timeline.Tests;

/// <summary>Writes ROFL2 files with real record and block framing (docs/research/rofl2-payload.md).</summary>
internal sealed class SyntheticReplay(int gameSeconds = 600)
{
    private readonly List<(RecordKind Kind, uint Id, byte[] Data, bool Compress)> records = [];
    private readonly List<Dictionary<string, string>> players = [];
    private int? corruptSizeOf;
    private int? garbleDataOf;
    private bool longHeaders;

    public SyntheticReplay WithRecord(RecordKind kind, uint id, byte[] data, bool compress = true)
    {
        records.Add((kind, id, data, compress));
        return this;
    }

    public SyntheticReplay WithPlayer(string name, int team, int deaths, int level, IReadOnlyDictionary<string, string>? extra = null)
    {
        var player = new Dictionary<string, string>
        {
            ["RIOT_ID_GAME_NAME"] = name,
            ["RIOT_ID_TAG_LINE"] = "EUW",
            ["SKIN"] = "Ahri",
            ["TEAM"] = team.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["WIN"] = team == 100 ? "Win" : "Fail",
            ["NUM_DEATHS"] = deaths.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["LEVEL"] = level.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        foreach (var (key, value) in extra ?? new Dictionary<string, string>())
        {
            player[key] = value;
        }

        players.Add(player);
        return this;
    }

    /// <summary>Makes the header of record <paramref name="index"/> claim more data than there is.</summary>
    public SyntheticReplay WithOversizedRecord(int index)
    {
        corruptSizeOf = index;
        return this;
    }

    /// <summary>Writes the record headers as patch 16.21 does: one more byte (4) before the data.</summary>
    public SyntheticReplay WithLongHeaders()
    {
        longHeaders = true;
        return this;
    }

    /// <summary>Makes record <paramref name="index"/> claim compression but hold bytes that are not zstd.</summary>
    public SyntheticReplay WithUndecodableRecord(int index)
    {
        garbleDataOf = index;
        return this;
    }

    public MemoryStream Build()
    {
        using var stream = new MemoryStream();
        stream.Write([.. "RIOT"u8, 2, 0, 0x14, 0x2c, 0xab, 0xd4, 0x5e, 0x45, 0xd8, 0x3f]);
        var version = "16.20.824.8524"u8;
        stream.WriteByte((byte)version.Length);
        stream.Write(version);

        using var compressor = new Compressor();
        var header = new byte[longHeaders ? 18 : 17];
        for (var i = 0; i < records.Count; i++)
        {
            var (kind, id, data, compress) = records[i];
            var stored = compress ? compressor.Wrap(data).ToArray() : data;
            if (garbleDataOf == i)
            {
                Array.Fill(stored, (byte)0x5A);
            }

            Array.Clear(header);
            if (longHeaders)
            {
                header[17] = 4;
            }

            BinaryPrimitives.WriteUInt32LittleEndian(header, id);
            header[4] = (byte)(id + 1);
            header[8] = (byte)kind;
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(9), (uint)data.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(13), compress ? (uint)stored.Length : 0);
            if (corruptSizeOf == i)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(13), 10_000_000);
            }

            stream.Write(header);
            stream.Write(stored);
        }

        stream.Write(new byte[256]);
        var metadata = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["gameLength"] = gameSeconds * 1000,
            ["statsJson"] = JsonSerializer.Serialize(players),
        }));
        stream.Write(metadata);
        Span<byte> footer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(footer, metadata.Length);
        stream.Write(footer);
        return new MemoryStream(stream.ToArray(), writable: false);
    }
}

/// <summary>Encodes blocks in the long form (all fields explicit) or the short form (deltas).</summary>
internal sealed class Blocks
{
    private readonly List<byte> data = [];

    /// <summary>Marker 0x00: f32 time, u32 length, u16 type, u32 parameter.</summary>
    public Blocks Full(float time, ushort type, uint parameter, params byte[] payload)
    {
        Span<byte> header = stackalloc byte[15];
        header[0] = 0x00;
        BinaryPrimitives.WriteSingleLittleEndian(header[1..], time);
        BinaryPrimitives.WriteUInt32LittleEndian(header[5..], (uint)payload.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(header[9..], type);
        BinaryPrimitives.WriteUInt32LittleEndian(header[11..], parameter);
        data.AddRange(header.ToArray());
        data.AddRange(payload);
        return this;
    }

    /// <summary>Marker 0xF0: u8 millisecond delta, u8 length, same type, u8 parameter delta.</summary>
    public Blocks Short(byte millisecondsLater, byte parameterDelta, params byte[] payload)
    {
        data.AddRange([0xF0, millisecondsLater, (byte)payload.Length, parameterDelta]);
        data.AddRange(payload);
        return this;
    }

    public byte[] ToArray() => [.. data];
}
