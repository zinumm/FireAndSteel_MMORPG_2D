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

    public async Task SendAsync(MessageType type, ReadOnlyMemory<byte> body, CancellationToken ct)
    {
        var seq = unchecked(++_sendSeq);
        var frame = FrameCodec.Encode(type, seq, body.Span);
        await _stream.WriteAsync(frame, ct);
        await _stream.FlushAsync(ct);
    }

    public Task SendAsync(MessageType type, byte[] body, CancellationToken ct)
        => SendAsync(type, (ReadOnlyMemory<byte>)body, ct);

    public async Task<(EnvelopeV1 env, byte[] body)> ReadAsync(CancellationToken ct)
    {
        var (env, body) = await FrameCodec.ReadAsync(_stream, ct);

        // rate limit consolidado aqui (um único lugar)
        var approxBytes = EnvelopeV1.HeaderSize + body.Length;
        if (!RateLimiter.TryConsume(messages: 1, bytes: approxBytes))
            throw new RateLimitExceededException(messages: 1, bytes: approxBytes);

        return (env, body);
    }

    public async Task TrySendDisconnectAsync(DisconnectReason reason, CancellationToken ct)
    {
        // Best-effort: não deixa exception de rede “vazar” no desligamento
        try
        {
            var msg = new Disconnect(reason);
            await SendAsync(MessageType.Disconnect, msg.ToBytes(), ct);
        }
        catch { }
    }

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

    public async ValueTask SendDisconnectAndCloseAsync(DisconnectReason reason, CancellationToken ct = default)
    {
        // Best-effort: tenta avisar o motivo antes de encerrar
        try
        {
            var disc = new Disconnect(reason);
            await SendAsync(MessageType.Disconnect, disc.ToBytes(), ct);
        }
        catch
        {
            // ignora: conexão pode já estar indo embora
        }

        // Fechamento gracioso: sinaliza fim de envio (evita RST em muitos casos)
        try { _client.Client.Shutdown(SocketShutdown.Send); } catch { }

        // Pequena janela para o pacote "sair" antes de fechar
        try { await Task.Delay(200, ct); } catch { }

        try { await DisposeAsync(); } catch { }
    }
}

