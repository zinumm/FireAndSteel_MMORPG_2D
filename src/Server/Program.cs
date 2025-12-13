using System.Net;
using System.Net.Sockets;
using FireAndSteel.Core.Data;
using FireAndSteel.Core.Data.Validation;
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

// DATA bootstrap (trava server se inválido)
var dataRoot = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Data");
dataRoot = Path.GetFullPath(dataRoot);

Console.WriteLine($"[Server] DataRoot: {dataRoot}");

DataStore store;
try
{
    store = DataLoader.LoadAll(dataRoot);
}
catch (Exception ex)
{
    Console.WriteLine("[Server] DATA LOAD FAILED");
    Console.WriteLine(ex.Message);
    Environment.ExitCode = 1;
    return;
}

var vr = Validator.ValidateAll(store);
if (!vr.Ok)
{
    Console.WriteLine("[Server] DATA INVALID:");
    foreach (var e in vr.Errors)
        Console.WriteLine($"- {e.Code} | {e.Path} | {e.Message}");

    Environment.ExitCode = 1;
    return;
}

Console.WriteLine("[Server] DATA OK.");


var ip = IPAddress.Parse(cfg.Network.Host);
var listener = new TcpListener(ip, cfg.Network.Port);

var router = new MessageRouter();
router.Register(MessageId.Ping, async (conn, env, body, ct) =>
{
    Console.WriteLine($"[Server] <- Ping seq={env.Seq} len={env.BodyLen}");
    await conn.SendAsync(MessageId.Pong, Array.Empty<byte>(), ct);
    Console.WriteLine("[Server] -> Pong");
});

listener.Start();
Console.WriteLine($"[Server] Listening on {cfg.Network.Host}:{cfg.Network.Port}");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

try
{
    while (!cts.IsCancellationRequested)
    {
        var tcp = await listener.AcceptTcpClientAsync(cts.Token);
        _ = Task.Run(async () =>
        {
            Console.WriteLine("[Server] Client connected.");
            await using var conn = new Connection(tcp, new RateLimiter(maxMsgsPerSec: 60, maxBytesPerSec: 64 * 1024));

            try
            {
                while (!cts.IsCancellationRequested)
                {
                    var (env, body) = await conn.ReadAsync(cts.Token);

                    var approxBytes = EnvelopeV1.HeaderSize + body.Length;
                    if (!conn.RateLimiter.TryConsume(1, approxBytes))
                        {
                            Console.WriteLine("[Server] Rate limit excedido.");
                            conn.Close();  // Fechar a conexão de forma limpa
                            return;  // Evitar continuar processando a conexão
                        }

                    await router.DispatchAsync(conn, env, body, cts.Token);
                }
            }
            catch (OperationCanceledException) { }
            catch (IOException)
            {
                // cliente encerrou a conexão (normal em console demo)
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Server] Conn drop: {ex.Message}");
            }

            conn.Close();
            Console.WriteLine("[Server] Client disconnected.");
        }, cts.Token);
    }
}
catch (OperationCanceledException) { }
finally
{
    listener.Stop();
}
