using System.Net.Sockets;

namespace FireAndSteel.Networking.Net;

public sealed class Connection : IAsyncDisposable
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;

    private uint _sendSeq = 0;

    public RateLimiter RateLimiter { get; }

    public Connection(TcpClient client, RateLimiter? limiter = null)
    {
        _client = client;
        _stream = client.GetStream();
        RateLimiter = limiter ?? new RateLimiter(maxMsgsPerSec: 60, maxBytesPerSec: 64 * 1024);
    }

    public async Task SendAsync(MessageId id, byte[] body, CancellationToken ct)
    {
        var seq = unchecked(++_sendSeq);
        var frame = FrameCodec.Encode(id, seq, body);
        await _stream.WriteAsync(frame, ct);
        await _stream.FlushAsync(ct);
    }

    public Task<(EnvelopeV1 env, byte[] body)> ReadAsync(CancellationToken ct)
        => FrameCodec.ReadAsync(_stream, ct);

    public void Close()
    {
        try { _stream.Close(); } catch { }
        try { _client.Close(); } catch { }
    }

    public ValueTask DisposeAsync()
    {
        Close();
        return ValueTask.CompletedTask;
    }
}
