using FireAndSteel.Networking.Net;
using Xunit;

namespace FireAndSteel.Tests;

public sealed class FrameCodecTests
{
    private sealed class ChunkyReadStream : Stream
    {
        private readonly byte[] _data;
        private int _pos;
        private readonly int _maxChunk;

        public ChunkyReadStream(byte[] data, int maxChunk)
        {
            _data = data;
            _maxChunk = Math.Max(1, maxChunk);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _data.Length;
        public override long Position { get => _pos; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_pos >= _data.Length) return 0;
            var n = Math.Min(count, Math.Min(_maxChunk, _data.Length - _pos));
            Buffer.BlockCopy(_data, _pos, buffer, offset, n);
            _pos += n;
            return n;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var arr = buffer.ToArray();
            var read = Read(arr, 0, arr.Length);
            arr.AsSpan(0, read).CopyTo(buffer.Span);
            return ValueTask.FromResult(read);
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    [Fact]
    public async Task ReadAsync_handles_partial_reads()
    {
        var hs = new Handshake(ProtocolVersion.V0, 123u, HandshakeStage.Request);
        var frame = FrameCodec.Encode(MessageType.Handshake, 1, hs.ToBytes());

        await using var s = new ChunkyReadStream(frame, maxChunk: 3);
        var (env, body) = await FrameCodec.ReadAsync(s, CancellationToken.None);

        Assert.Equal((ushort)ProtocolVersion.V0, env.Version);
        Assert.Equal(MessageType.Handshake, env.MessageType);
        var decoded = Handshake.Read(body);
        Assert.Equal(ProtocolVersion.V0, decoded.Version);
        Assert.Equal(123u, decoded.Nonce);
        Assert.Equal(HandshakeStage.Request, decoded.Stage);
    }

    [Fact]
    public void Encode_throws_when_body_exceeds_limit()
    {
        var tooBig = new byte[ProtocolConstants.MaxBodyBytes + 1];
        Assert.Throws<InvalidOperationException>(() =>
            FrameCodec.Encode(MessageType.Ping, 1, tooBig));
    }

    [Fact]
    public async Task ReadAsync_throws_when_bodylen_exceeds_limit()
    {
        // Header com BodyLen acima do limite; stream só precisa fornecer o header.
        var header = new byte[EnvelopeV1.HeaderSize];
        var env = new EnvelopeV1(ProtocolConstants.CurrentProtocolVersion, 0, MessageType.Ping, 1, (uint)ProtocolConstants.MaxBodyBytes + 1);
        env.Write(header);

        await using var s = new MemoryStream(header);
        await Assert.ThrowsAsync<InvalidOperationException>(() => FrameCodec.ReadAsync(s, CancellationToken.None));
    }
}
