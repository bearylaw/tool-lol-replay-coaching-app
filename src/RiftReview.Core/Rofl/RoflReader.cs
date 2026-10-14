using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using RiftReview.Core.Matches;
using RiftReview.Core.Results;

namespace RiftReview.Core.Rofl;

/// <summary>
/// Reads the match metadata of a ROFL2 replay (the format since 2024) without touching the
/// game data in between. The layout is documented in <c>docs/rofl-format.md</c>.
/// </summary>
public static class RoflReader
{
    /// <summary>The bytes every ROFL2 file starts with: <c>RIOT</c>, then format version 2.</summary>
    public static ReadOnlySpan<byte> Signature => "RIOT\u0002\u0000"u8;

    private const int VersionLengthOffset = 14;
    private const int VersionOffset = 15;
    private const int MaxVersionLength = 32;
    private const int FooterLengthSize = 4;

    // Real metadata is about 100 KB. The cap keeps a corrupt length from allocating gigabytes.
    private const int MaxMetadataLength = 16 * 1024 * 1024;

    /// <summary>
    /// Reads a replay's metadata from a seekable stream. Expected failures (not a replay, other
    /// format, truncated, malformed) come back as a <see cref="RoflError"/>; I/O errors throw.
    /// </summary>
    public static Result<ReplayMetadata, RoflError> Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanSeek)
        {
            throw new ArgumentException("The replay stream must be seekable.", nameof(stream));
        }

        var header = new byte[VersionOffset + MaxVersionLength];
        stream.Position = 0;
        var headerLength = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);

        if (headerLength < Signature.Length || !header.AsSpan(0, 4).SequenceEqual("RIOT"u8))
        {
            return Fail(RoflErrorKind.NotAReplay, "missing the RIOT signature");
        }

        if (!header.AsSpan(0, Signature.Length).SequenceEqual(Signature))
        {
            return Fail(RoflErrorKind.UnsupportedFormat, $"container version byte {header[4]}, expected 2");
        }

        var version = ReadVersion(header.AsSpan(0, headerLength));
        if (version is null)
        {
            return Fail(RoflErrorKind.InvalidMetadata, "no game version at header offset 15");
        }

        var json = ReadMetadataJson(stream, VersionOffset + MaxVersionLength);
        return json.TryGetValue(out var metadataJson, out var error)
            ? ParseMetadata(metadataJson, version.Value)
            : Result<ReplayMetadata, RoflError>.Failure(error);
    }

    /// <summary>Size of the block between the game data and the metadata (probably a signature).</summary>
    public const int TrailerLength = 256;

    /// <summary>
    /// Where the game data (the chunk and keyframe records) sits in a ROFL2 file: from right after
    /// the version string up to the 256-byte trailer before the metadata.
    /// </summary>
    public static Result<(long Start, long End), RoflError> GetGameDataRange(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var header = new byte[VersionOffset];
        stream.Position = 0;
        if (stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false) < header.Length
            || !header.AsSpan(0, Signature.Length).SequenceEqual(Signature))
        {
            return Result<(long, long), RoflError>.Failure(new(RoflErrorKind.NotAReplay, "missing the ROFL2 signature"));
        }

        Span<byte> footer = stackalloc byte[FooterLengthSize];
        stream.Position = stream.Length - FooterLengthSize;
        stream.ReadExactly(footer);
        var start = (long)VersionOffset + header[VersionLengthOffset];
        var end = stream.Length - FooterLengthSize - BinaryPrimitives.ReadUInt32LittleEndian(footer) - TrailerLength;
        return end < start
            ? Result<(long, long), RoflError>.Failure(new(RoflErrorKind.Truncated, $"game data would end at {end}, before it starts at {start}"))
            : Result<(long, long), RoflError>.Success((start, end));
    }

    private static GameVersion? ReadVersion(ReadOnlySpan<byte> header)
    {
        if (header.Length <= VersionLengthOffset)
        {
            return null;
        }

        int length = header[VersionLengthOffset];
        if (length is 0 or > MaxVersionLength || header.Length < VersionOffset + length)
        {
            return null;
        }

        return GameVersion.TryParse(Encoding.ASCII.GetString(header.Slice(VersionOffset, length)));
    }

    private static Result<byte[], RoflError> ReadMetadataJson(Stream stream, int minimumFileLength)
    {
        var fileLength = stream.Length;
        if (fileLength < minimumFileLength + FooterLengthSize)
        {
            return Result<byte[], RoflError>.Failure(new(RoflErrorKind.Truncated, $"file is only {fileLength} bytes"));
        }

        Span<byte> footer = stackalloc byte[FooterLengthSize];
        stream.Position = fileLength - FooterLengthSize;
        stream.ReadExactly(footer);
        var metadataLength = BinaryPrimitives.ReadUInt32LittleEndian(footer);

        var available = fileLength - FooterLengthSize - minimumFileLength;
        if (metadataLength == 0 || metadataLength > MaxMetadataLength || metadataLength > available)
        {
            return Result<byte[], RoflError>.Failure(new(
                RoflErrorKind.Truncated,
                $"metadata length {metadataLength} does not fit a {fileLength}-byte file"));
        }

        var json = new byte[metadataLength];
        stream.Position = fileLength - FooterLengthSize - metadataLength;
        stream.ReadExactly(json);
        return Result<byte[], RoflError>.Success(json);
    }

    private static Result<ReplayMetadata, RoflError> ParseMetadata(byte[] json, GameVersion version)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Fail(RoflErrorKind.InvalidMetadata, "metadata is not a JSON object");
            }

            if (!root.TryGetProperty("gameLength", out var lengthElement)
                || !lengthElement.TryGetInt64(out var lengthMs)
                || lengthMs < 0)
            {
                return Fail(RoflErrorKind.InvalidMetadata, "metadata has no valid gameLength");
            }

            if (!root.TryGetProperty("statsJson", out var statsElement)
                || statsElement.ValueKind != JsonValueKind.String)
            {
                return Fail(RoflErrorKind.InvalidMetadata, "metadata has no statsJson string");
            }

            var players = PlayerStatsParser.Parse(statsElement.GetString()!);
            return players.TryGetValue(out var parsed, out var error)
                ? Result<ReplayMetadata, RoflError>.Success(new ReplayMetadata
                {
                    Version = version,
                    GameLength = TimeSpan.FromMilliseconds(lengthMs),
                    Players = parsed,
                })
                : Fail(RoflErrorKind.InvalidMetadata, error);
        }
        catch (JsonException exception)
        {
            return Fail(RoflErrorKind.InvalidMetadata, $"metadata is not valid JSON: {exception.Message}");
        }
    }

    private static Result<ReplayMetadata, RoflError> Fail(RoflErrorKind kind, string detail) =>
        Result<ReplayMetadata, RoflError>.Failure(new RoflError(kind, detail));
}
