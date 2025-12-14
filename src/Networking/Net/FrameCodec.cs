namespace FireAndSteel.Networking.Net;

public static class FrameCodec
{
    public static byte[] Encode(MessageType type, uint seq, ReadOnlySpan<byte> body, ushort flags = 0)
    {
        if (body.Length > ProtocolConstants.MaxBodyBytes)
            throw new InvalidOperationException("Body excede limite.");

        var header = new byte[EnvelopeV1.HeaderSize];
        var env = new EnvelopeV1(ProtocolConstants.CurrentProtocolVersion, flags, type, seq, (uint)body.Length);
        env.Write(header);

        var frame = new byte[EnvelopeV1.HeaderSize + body.Length];
        Buffer.BlockCopy(header, 0, frame, 0, EnvelopeV1.HeaderSize);
        body.CopyTo(frame.AsSpan(EnvelopeV1.HeaderSize));

        return frame;
    }

    public static async Task<(EnvelopeV1 env, byte[] body)> ReadAsync(Stream stream, CancellationToken ct)
    {
        var header = await ReadExactAsync(stream, EnvelopeV1.HeaderSize, ct);
        var env = EnvelopeV1.Read(header);

        if (env.Version != ProtocolConstants.CurrentProtocolVersion)
            throw new InvalidOperationException($"Versão de protocolo inválida: {env.Version}");

        if (env.BodyLen > ProtocolConstants.MaxBodyBytes)
            throw new InvalidOperationException($"BodyLen inválido: {env.BodyLen}");

        var body = env.BodyLen == 0
            ? Array.Empty<byte>()
            : await ReadExactAsync(stream, (int)env.BodyLen, ct);

        return (env, body);
    }

    private static async Task<byte[]> ReadExactAsync(Stream stream, int n, CancellationToken ct)
    {
        var buf = new byte[n];
        var off = 0;
        while (off < n)
        {
            var read = await stream.ReadAsync(buf.AsMemory(off, n - off), ct);
            if (read == 0) throw new IOException("Conexão fechada durante leitura.");
            off += read;
        }
        return buf;
    }
}
