using System.Net.Sockets;
using FireAndSteel.Core.Config;
using FireAndSteel.Networking.Net;
using System.IO;

static string GetArg(string[] args, string key, string fallback)
{
    var idx = Array.IndexOf(args, key);
    if (idx >= 0 && idx + 1 < args.Length) return args[idx + 1];
    return fallback;
}

var configPath = GetArg(args, "--config", Path.Combine("Config", "runtime.json"));
var cfg = JsonConfig.LoadRuntime(configPath);

Console.WriteLine($"[Client] Connecting to {cfg.Network.Host}:{cfg.Network.Port} ...");

using var tcp = new TcpClient();
await tcp.ConnectAsync(cfg.Network.Host, cfg.Network.Port);
await using var conn = new Connection(tcp);

Console.WriteLine("[Client] Connected. Spamming Ping...");

var sent = 0;
try
{
    for (var i = 0; i < 5000; i++)
    {
        await conn.SendAsync(MessageId.Ping, Array.Empty<byte>(), CancellationToken.None);
        sent++;

        // opcional: sem delay estoura mais rápido; com delay fica mais “real”
        // await Task.Delay(1);
    }
}
catch (IOException)
{
    Console.WriteLine("[Client] Conexão fechada pelo servidor durante envio (esperado no rate limit).");
}
catch (SocketException)
{
    Console.WriteLine("[Client] Conexão resetada pelo servidor durante envio (esperado no rate limit).");
}

Console.WriteLine($"[Client] Sent={sent}. Waiting 2s...");
await Task.Delay(2000);

// Tenta ler o que vier até cair
var pongs = 0;
try
{
    while (true)
    {
        var (respEnv, _) = await conn.ReadAsync(CancellationToken.None);
        if (respEnv.MessageId == MessageId.Pong)
        {
            pongs++;
            if (pongs <= 10)
                Console.WriteLine($"[Client] <- Pong seq={respEnv.Seq} total={pongs}");
        }
    }
}
catch (IOException)
{
    Console.WriteLine("[Client] Leitura encerrada (server fechou).");
}
catch (SocketException)
{
    Console.WriteLine("[Client] Leitura encerrada (reset).");
}

Console.WriteLine($"[Client] Done. Pongs={pongs}");
