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

var dataRoot = ResolveDataRoot();
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
router.Register(MessageType.Ping, async (conn, env, body, ct) =>
{
    // Sem spam de log por mensagem (Sprint 1)
    await conn.SendAsync(MessageType.Pong, Array.Empty<byte>(), ct);
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
	            string? disconnectNote = null;
            await using var conn = new Connection(tcp, new RateLimiter(maxMsgsPerSec: 60, maxBytesPerSec: 64 * 1024));

            try
            {
                // 1) Handshake obrigatório (Sprint 1)
                var (hsEnv, hsBody) = await conn.ReadAsync(cts.Token);
                if (hsEnv.MessageType != MessageType.Handshake)
                {
                    Console.WriteLine("[Server] Bad handshake: primeiro pacote não é Handshake.");
                    await conn.TrySendDisconnectAsync(DisconnectReason.BadHandshake, cts.Token);
                    return;
                }

                var hs = Handshake.Read(hsBody);
                if (hs.Stage != HandshakeStage.Request || hs.Version != ProtocolVersion.V0)
                {
                    Console.WriteLine($"[Server] Bad handshake: stage={hs.Stage} version={hs.Version}.");
                    await conn.TrySendDisconnectAsync(DisconnectReason.BadHandshake, cts.Token);
                    return;
                }

                var ack = new Handshake(ProtocolVersion.V0, hs.Nonce, HandshakeStage.Ack);
                await conn.SendAsync(MessageType.Handshake, ack.ToBytes(), cts.Token);
                Console.WriteLine("[Server] Handshake OK.");

                // 2) Loop de mensagens
                while (!cts.IsCancellationRequested)
                {
                    var (env, body) = await conn.ReadAsync(cts.Token);

	                    if (env.MessageType == MessageType.Disconnect)
                    {
                        var d = Disconnect.Read(body);
                        disconnectNote = $"[Server] Client disconnected (reason={d.Reason}).";
                        return;
                    }

                    await router.DispatchAsync(conn, env, body, cts.Token);
                }
            }
            catch (RateLimitExceededException)
            {
                Console.WriteLine("[Server] Rate limit excedido.");
                await conn.SendDisconnectAndCloseAsync(DisconnectReason.RateLimit);
                Console.WriteLine("[Server] Client disconnected (reason=RateLimit).");
                return;
            }
            catch (InvalidOperationException ex)
            {
	                disconnectNote = "[Server] Client disconnected (reason=ProtocolError).";
                Console.WriteLine($"[Server] Protocol error: {ex.Message}");
                await conn.TrySendDisconnectAsync(DisconnectReason.ProtocolError, cts.Token);
            }
            catch (OperationCanceledException) { }
            catch (IOException)
            {
                // cliente encerrou a conexão (normal em console demo)
	                disconnectNote ??= "[Server] Client disconnected (io).";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Server] Conn drop: {ex.Message}");
            }
            finally
            {
                conn.Close();
	                Console.WriteLine(disconnectNote ?? "[Server] Client disconnected.");
            }
        }, cts.Token);
    }
}
catch (OperationCanceledException) { }
finally
{
    listener.Stop();
}

static string ResolveDataRoot()
{
    var baseDir = AppContext.BaseDirectory;

    var repoRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));
    var candidates = new[]
    {
        Path.Combine(repoRoot, "Data"),
        Path.Combine(repoRoot, "src", "Data"),
    };

    foreach (var c in candidates)
        if (Directory.Exists(c))
            return c;

    throw new DirectoryNotFoundException(
        "Pasta Data não encontrada. Tentativas:\n- " + string.Join("\n- ", candidates));
}
