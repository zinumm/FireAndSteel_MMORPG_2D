using System.Buffers.Binary;

namespace FireAndSteel.Networking.Net;

// Envelope fixo (little-endian):
// [u16 version][u16 flags][u16 msgId][u16 reserved]
// [u32 seq][u32 bodyLen]
// Total header = 2+2+2+2+4+4 = 16 bytes
public readonly struct EnvelopeV1
{
    public const int HeaderSize = 16;

    public readonly ushort Version;
    public readonly ushort Flags;
    public readonly MessageType MessageType;
    public readonly uint Seq;
    public readonly uint BodyLen;

    public EnvelopeV1(ushort version, ushort flags, MessageType messageType, uint seq, uint bodyLen)
    {
        Version = version;
        Flags = flags;
        MessageType = messageType;
        Seq = seq;
        BodyLen = bodyLen;
    }

    public static EnvelopeV1 Read(ReadOnlySpan<byte> header)
    {
        if (header.Length != HeaderSize)
            throw new ArgumentException("Header size inválido.");

        var version = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(0, 2));
        var flags   = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(2, 2));
        var msgId   = (MessageType)BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(4, 2));
        // reserved header.Slice(6,2)
        var seq     = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(8, 4));
        var bodyLen = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(12, 4));

        return new EnvelopeV1(version, flags, msgId, seq, bodyLen);
    }

    public void Write(Span<byte> header)
    {
        if (header.Length != HeaderSize)
            throw new ArgumentException("Header size inválido.");

        BinaryPrimitives.WriteUInt16LittleEndian(header.Slice(0, 2), Version);
        BinaryPrimitives.WriteUInt16LittleEndian(header.Slice(2, 2), Flags);
        BinaryPrimitives.WriteUInt16LittleEndian(header.Slice(4, 2), (ushort)MessageType);
        BinaryPrimitives.WriteUInt16LittleEndian(header.Slice(6, 2), 0); // reserved
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(8, 4), Seq);
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(12, 4), BodyLen);
    }
}
