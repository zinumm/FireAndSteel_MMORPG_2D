using System.Buffers.Binary;

namespace FireAndSteel.Networking.Net;

// Corpo do Disconnect (2 bytes, little-endian):
// [u16 reason]
public readonly struct Disconnect
{
    public const int Size = 2;

    public readonly DisconnectReason Reason;

    public Disconnect(DisconnectReason reason)
    {
        Reason = reason;
    }

    public static Disconnect Read(ReadOnlySpan<byte> body)
    {
        if (body.Length < Size)
            throw new InvalidOperationException("Disconnect body inválido.");

        var r = (DisconnectReason)BinaryPrimitives.ReadUInt16LittleEndian(body.Slice(0, 2));
        return new Disconnect(r);
    }

    public void Write(Span<byte> body)
    {
        if (body.Length < Size)
            throw new InvalidOperationException("Disconnect body inválido.");

        BinaryPrimitives.WriteUInt16LittleEndian(body.Slice(0, 2), (ushort)Reason);
    }

    public byte[] ToBytes()
    {
        var buf = new byte[Size];
        Write(buf);
        return buf;
    }
}
