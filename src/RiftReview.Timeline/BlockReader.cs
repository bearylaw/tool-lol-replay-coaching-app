using System.Buffers.Binary;

namespace RiftReview.Timeline;

/// <summary>One block (packet) of a decompressed record.</summary>
/// <param name="Time">Seconds since the game started.</param>
/// <param name="Type">Packet type; its meaning changes per patch and sometimes per game.</param>
/// <param name="Parameter">Network id of the unit the packet belongs to, 0 for none.</param>
/// <param name="PayloadOffset">Where the payload starts in the record's data.</param>
/// <param name="PayloadLength">Payload length in bytes.</param>
public readonly record struct Block(float Time, ushort Type, uint Parameter, int PayloadOffset, int PayloadLength);

/// <summary>
/// Walks the blocks of a decompressed record. A marker byte says how wide the following fields are:
/// 0x80 time as a u8 millisecond delta (else f32 seconds), 0x10 length as u8 (else u32), 0x40 the
/// previous packet type (else u16), 0x20 parameter as a u8 delta (else u32).
/// </summary>
public ref struct BlockReader(ReadOnlySpan<byte> data)
{
    private readonly ReadOnlySpan<byte> data = data;
    private int position;
    private float time;
    private ushort type;
    private uint parameter;

    /// <summary>Set when the data ended inside a block; the reader then stops.</summary>
    public string? Error { get; private set; }

    /// <summary>Reads the next block; false at the end of the data or on malformed data (see <see cref="Error"/>).</summary>
    public bool TryRead(out Block block)
    {
        block = default;
        if (Error is not null || position >= data.Length)
        {
            return false;
        }

        var start = position;
        var marker = data[position++];
        if (!TryTime(marker) || !TryLength(marker, out var length) || !TryType(marker) || !TryParameter(marker))
        {
            Error = $"block at {start} runs past the end of {data.Length} bytes";
            return false;
        }

        if (length > data.Length - position)
        {
            Error = $"block at {start} declares {length} payload bytes, {data.Length - position} left";
            return false;
        }

        block = new Block(time, type, parameter, position, length);
        position += length;
        return true;
    }

    private bool TryTime(byte marker)
    {
        if ((marker & 0x80) != 0)
        {
            if (!Has(1))
            {
                return false;
            }

            time += data[position++] / 1000f;
            return true;
        }

        if (!Has(4))
        {
            return false;
        }

        time = BinaryPrimitives.ReadSingleLittleEndian(data[position..]);
        position += 4;
        return true;
    }

    private bool TryLength(byte marker, out int length)
    {
        length = 0;
        if ((marker & 0x10) != 0)
        {
            if (!Has(1))
            {
                return false;
            }

            length = data[position++];
            return true;
        }

        if (!Has(4))
        {
            return false;
        }

        var value = BinaryPrimitives.ReadUInt32LittleEndian(data[position..]);
        position += 4;
        length = value > int.MaxValue ? int.MaxValue : (int)value;
        return true;
    }

    private bool TryType(byte marker)
    {
        if ((marker & 0x40) != 0)
        {
            return true;
        }

        if (!Has(2))
        {
            return false;
        }

        type = BinaryPrimitives.ReadUInt16LittleEndian(data[position..]);
        position += 2;
        return true;
    }

    private bool TryParameter(byte marker)
    {
        if ((marker & 0x20) != 0)
        {
            if (!Has(1))
            {
                return false;
            }

            parameter += data[position++];
            return true;
        }

        if (!Has(4))
        {
            return false;
        }

        parameter = BinaryPrimitives.ReadUInt32LittleEndian(data[position..]);
        position += 4;
        return true;
    }

    private readonly bool Has(int count) => data.Length - position >= count;
}
