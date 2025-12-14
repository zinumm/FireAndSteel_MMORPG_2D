using System.Buffers.Binary;

namespace FireAndSteel.Networking.Net;

public enum HandshakeStage : byte
{
    Request = 0,
    Ack = 1,
}

// Corpo do Handshake (8 bytes, little-endian):
// [u16 protocolVersion][u32 nonce][u8 stage][u8 reserved]
public readonly struct Handshake
{
    public const int Size = 8;

    public readonly ProtocolVersion Version;
    public readonly uint Nonce;
    public readonly HandshakeStage Stage;

    public Handshake(ProtocolVersion version, uint nonce, HandshakeStage stage)
    {
        Version = version;
        Nonce = nonce;
        Stage = stage;
    }

    public static Handshake Read(ReadOnlySpan<byte> body)
    {
        if (body.Length < Size)
            throw new InvalidOperationException("Handshake body inválido.");

        var ver = (ProtocolVersion)BinaryPrimitives.ReadUInt16LittleEndian(body.Slice(0, 2));
        var nonce = BinaryPrimitives.ReadUInt32LittleEndian(body.Slice(2, 4));
        var stage = (HandshakeStage)body[6];
        return new Handshake(ver, nonce, stage);
    }

    public void Write(Span<byte> body)
    {
        if (body.Length < Size)
            throw new InvalidOperationException("Handshake body inválido.");

        BinaryPrimitives.WriteUInt16LittleEndian(body.Slice(0, 2), (ushort)Version);
        BinaryPrimitives.WriteUInt32LittleEndian(body.Slice(2, 4), Nonce);
        body[6] = (byte)Stage;
        body[7] = 0;
    }

    public byte[] ToBytes()
    {
        var buf = new byte[Size];
        Write(buf);
        return buf;
    }
}
