using FireAndSteel.Core.Config;
using FireAndSteel.Core.Data;
using FireAndSteel.Core.Data.Validation;
using FireAndSteel.Networking.Net;
using FireAndSteel.Server.Net;

static string GetArg(string[] args, string key, string fallback)
{
    var idx = Array.IndexOf(args, key);
    if (idx >= 0 && idx + 1 < args.Length) return args[idx + 1];
    return fallback;
}

static int GetPort(string[] args, RuntimeConfig cfg)
{
    var portStr = GetArg(args, "--port", cfg.Network.Port.ToString());
    if (!int.TryParse(portStr, out var port) || port < 1 || port > 65535)
        throw new InvalidOperationException($"Port inválida: '{portStr}' (range 1..65535).");
    return port;
}

var configPath = GetArg(args, "--config", Path.Combine("Config", "runtime.json"));
var cfg = JsonConfig.LoadRuntime(configPath);

// Overrides (Sprint 2)
var host = GetArg(args, "--host", cfg.Network.Host);
var port = GetPort(args, cfg);

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

var router = new MessageRouter();
router.Register(MessageType.Ping, async (conn, env, body, ct) =>
{
    // Sem spam de log por mensagem (Sprint 1)
    await conn.SendAsync(MessageType.Pong, Array.Empty<byte>(), ct);
});

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

await using var server = new ServerHost(
    host: host,
    port: port,
    router: router,
    logger: msg => Console.WriteLine(msg),
    handshakeTimeout: TimeSpan.FromSeconds(2));

server.Start(cts.Token);

try
{
    // mantém o processo vivo até Ctrl+C
    await Task.Delay(Timeout.InfiniteTimeSpan, cts.Token);
}
catch (OperationCanceledException) { }
finally
{
    await server.StopAsync(CancellationToken.None);
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
