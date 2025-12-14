using System.Net.Sockets;
using System.IO;
using FireAndSteel.Core.Config;
using FireAndSteel.Networking.Net;

static string GetArg(string[] args, string key, string fallback)
{
    var idx = Array.IndexOf(args, key);
    if (idx >= 0 && idx + 1 < args.Length) return args[idx + 1];
    return fallback;
}

var configPath = GetArg(args, "--config", Path.Combine("Config", "runtime.json"));
var cfg = JsonConfig.LoadRuntime(configPath);

var spam = args.Contains("--spam", StringComparer.OrdinalIgnoreCase);
var count = int.TryParse(GetArg(args, "--count", spam ? "5000" : "25"), out var c) ? c : (spam ? 5000 : 25);
var delayMs = int.TryParse(GetArg(args, "--delayMs", spam ? "0" : "10"), out var d) ? d : (spam ? 0 : 10);

Console.WriteLine($"[Client] Connecting to {cfg.Network.Host}:{cfg.Network.Port} ...");

using var tcp = new TcpClient();
await tcp.ConnectAsync(cfg.Network.Host, cfg.Network.Port);
await using var conn = new Connection(tcp);

// 1) Handshake (Sprint 1)
var nonce = (uint)Random.Shared.Next();
var hsReq = new Handshake(ProtocolVersion.V0, nonce, HandshakeStage.Request);
await conn.SendAsync(MessageType.Handshake, hsReq.ToBytes(), CancellationToken.None);

try
{
    using var hsCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
    while (true)
    {
        var (env, body) = await conn.ReadAsync(hsCts.Token);

        if (env.MessageType == MessageType.Disconnect)
        {
            var disc = Disconnect.Read(body);
            Console.WriteLine($"[Client] Disconnected (reason={disc.Reason}).");
            return;
        }

        if (env.MessageType == MessageType.Handshake)
        {
            var hsAck = Handshake.Read(body);
            if (hsAck.Stage == HandshakeStage.Ack && hsAck.Version == ProtocolVersion.V0 && hsAck.Nonce == nonce)
                break;
        }
    }

    Console.WriteLine("[Client] Handshake OK.");
}
catch (OperationCanceledException)
{
    Console.WriteLine("[Client] Handshake timeout.");
    return;
}

/// 2) Ping/Pong demo (Sprint 1) — receiver em paralelo para capturar DisconnectReason
Console.WriteLine(spam
    ? "[Client] Spamming Ping..."
    : "[Client] Sending Ping..."
);

var sent = 0;
var pongs = 0;

using var stopCts = new CancellationTokenSource();
DisconnectReason? reason = null;

// Receiver: fica lendo enquanto o sender está ativo
var recvTask = Task.Run(async () =>
{
    try
    {
        while (!stopCts.IsCancellationRequested)
        {
            var (env, body) = await conn.ReadAsync(stopCts.Token);

            if (env.MessageType == MessageType.Pong)
            {
                pongs++;
                continue;
            }

            if (env.MessageType == MessageType.Disconnect)
            {
                var disc = Disconnect.Read(body);
                reason = disc.Reason;
                Console.WriteLine($"[Client] Disconnected (reason={disc.Reason}).");
                stopCts.Cancel(); // sinaliza pro sender parar
                break;
            }
        }
    }
    catch (OperationCanceledException) { /* ok */ }
    catch (IOException) { /* conexão pode fechar */ }
    catch (SocketException) { /* idem */ }
}, CancellationToken.None);

// Sender
try
{
    for (var i = 0; i < count && !stopCts.IsCancellationRequested; i++)
    {
        await conn.SendAsync(MessageType.Ping, Array.Empty<byte>(), CancellationToken.None);
        sent++;

        if (delayMs > 0) await Task.Delay(delayMs);
    }
}
catch (IOException)
{
    Console.WriteLine("[Client] Conexão fechada pelo servidor durante envio.");
}
catch (SocketException)
{
    Console.WriteLine("[Client] Conexão resetada pelo servidor durante envio.");
}
finally
{
    // Dá tempo do receiver pegar o Disconnect que já pode estar no buffer
    await Task.WhenAny(recvTask, Task.Delay(3000));
    stopCts.Cancel();
    try { await recvTask; } catch { }
}

Console.WriteLine($"[Client] Sent={sent} Pongs={pongs}" + (reason is not null ? $" Reason={reason}" : ""));

