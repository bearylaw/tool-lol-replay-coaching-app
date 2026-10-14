using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace RiftReview.Core.Tests.Rofl;

/// <summary>Builds synthetic ROFL2 files with the layout of real ones (see docs/rofl-format.md).</summary>
internal sealed class RoflFileBuilder
{
    // Bytes 6-13 are identical in every replay seen so far; their meaning is unknown.
    private static readonly byte[] UnknownHeaderBytes = [0x14, 0x2c, 0xab, 0xd4, 0x5e, 0x45, 0xd8, 0x3f];

    private byte[] signature = [.. "RIOT"u8, 0x02, 0x00];
    private string version = "16.20.824.8524";
    private string? metadataJson;
    private long gameLengthMs = 1_506_188;
    private readonly List<Dictionary<string, string>> players = [];
    private int? footerOverride;

    public static RoflFileBuilder AReplay() => new();

    public static Dictionary<string, string> APlayer(string name, string champion, int team, bool won) => new()
    {
        ["RIOT_ID_GAME_NAME"] = name,
        ["RIOT_ID_TAG_LINE"] = "EUW",
        ["SKIN"] = champion,
        ["TEAM"] = team.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["TEAM_POSITION"] = "MIDDLE",
        ["WIN"] = won ? "Win" : "Fail",
        ["CHAMPIONS_KILLED"] = "9",
        ["NUM_DEATHS"] = "7",
        ["ASSISTS"] = "11",
        ["MINIONS_KILLED"] = "201",
        ["NEUTRAL_MINIONS_KILLED"] = "12",
        ["ITEM0"] = "3115",
        ["ITEM6"] = "3363",
        ["SUMMONER_SPELL_1"] = "4",
        ["SUMMONER_SPELL_2"] = "12",
        ["PERK0"] = "8112",
    };

    public RoflFileBuilder WithSignature(params byte[] bytes)
    {
        signature = bytes;
        return this;
    }

    public RoflFileBuilder WithVersion(string value)
    {
        version = value;
        return this;
    }

    public RoflFileBuilder WithGameLengthMs(long value)
    {
        gameLengthMs = value;
        return this;
    }

    public RoflFileBuilder WithPlayer(Dictionary<string, string> player)
    {
        players.Add(player);
        return this;
    }

    public RoflFileBuilder WithMetadataJson(string json)
    {
        metadataJson = json;
        return this;
    }

    public RoflFileBuilder WithFooterLength(int length)
    {
        footerOverride = length;
        return this;
    }

    public byte[] Build()
    {
        var versionBytes = Encoding.ASCII.GetBytes(version);
        var metadata = Encoding.UTF8.GetBytes(metadataJson ?? DefaultMetadata());
        // Stand-in for the compressed game data that sits between header and metadata.
        var payload = new byte[256];

        using var stream = new MemoryStream();
        stream.Write(signature);
        stream.Write(UnknownHeaderBytes);
        stream.WriteByte((byte)versionBytes.Length);
        stream.Write(versionBytes);
        stream.Write(payload);
        stream.Write(metadata);
        Span<byte> footer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(footer, footerOverride ?? metadata.Length);
        stream.Write(footer);
        return stream.ToArray();
    }

    public MemoryStream BuildStream() => new(Build(), writable: false);

    private string DefaultMetadata() => JsonSerializer.Serialize(new Dictionary<string, object>
    {
        ["gameLength"] = gameLengthMs,
        ["lastGameChunkId"] = 42,
        ["lastKeyFrameId"] = 21,
        ["statsJson"] = JsonSerializer.Serialize(players),
    });
}
