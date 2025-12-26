using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using FireAndSteel.Networking.Net;

namespace FireAndSteel.Server.Net;

public sealed class ServerHost : IAsyncDisposable
{
    private const int MaxTrackedClientTasks = 10_000;
    private static readonly TimeSpan DefaultClientDrainTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan DefaultReadIdleTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DefaultWriteTimeout = TimeSpan.FromSeconds(5);

    private readonly string _host;
    private readonly int _port;
    private int _stopOnce;
    private readonly TimeSpan _handshakeTimeout;
    private readonly TimeSpan _readIdleTimeout;
    private readonly TimeSpan _writeTimeout;
    private readonly TimeSpan _clientDrainTimeout;
    private readonly Action<string> _log;
    private readonly MessageRouter _router;

    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptLoopTask;

    private readonly ConcurrentDictionary<long, Task> _clientTasks = new();
    private readonly ServerMetrics _metrics = new();

    public SessionManager Sessions { get; } = new();

    public IPEndPoint? BoundEndPoint { get; private set; }

    internal ServerMetrics Metrics => _metrics;

    public ServerHost(
        string host,
        int port,
        MessageRouter router,
        Action<string>? logger = null,
        TimeSpan? handshakeTimeout = null,
        TimeSpan? readIdleTimeout = null,
        TimeSpan? writeTimeout = null,
        TimeSpan? clientDrainTimeout = null)
    {
        _host = host;
        _port = port;
        _router = router ?? throw new ArgumentNullException(nameof(router));
        _log = logger ?? (_ => { });
        _handshakeTimeout = handshakeTimeout ?? TimeSpan.FromSeconds(2);
        _readIdleTimeout = readIdleTimeout ?? DefaultReadIdleTimeout;
        _writeTimeout = writeTimeout ?? DefaultWriteTimeout;
        _clientDrainTimeout = clientDrainTimeout ?? DefaultClientDrainTimeout;
    }

    public void Start(CancellationToken ct = default)
    {
        if (_listener is not null)
            throw new InvalidOperationException("ServerHost já iniciado.");

        var ip = ResolveIp(_host);
        _listener = new TcpListener(ip, _port);
        _listener.Start();

        var local = _listener.LocalEndpoint;
        if (local is null)
        throw new InvalidOperationException("Listener LocalEndpoint nulo após Start().");

        if (local is not IPEndPoint ep)
        throw new InvalidOperationException($"LocalEndpoint inesperado: {local.GetType().Name}");

        BoundEndPoint = ep;

        LogInfo("server_start",
            ("host", _host),
            ("port", _port),
            ("bound", $"{BoundEndPoint.Address}:{BoundEndPoint.Port}"));

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _acceptLoopTask = AcceptLoopAsync(_cts.Token);
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        if (Interlocked.Exchange(ref _stopOnce, 1) != 0)
            return;
        
        if (_cts is null)
            return;

        LogInfo("server_stop_begin",
            ("trackedTasks", _clientTasks.Count),
            ("sessions", Sessions.Count));

        try { _cts.Cancel(); } catch { }

        try { _listener?.Stop(); } catch { }
        _listener = null;

        if (_acceptLoopTask is not null)
            await AwaitQuietlyAsync(_acceptLoopTask, ct).ConfigureAwait(false);

        await DrainClientsBestEffortAsync(ct).ConfigureAwait(false);

        LogInfo("server_stop_end",
            ("trackedTasks", _clientTasks.Count),
            ("sessions", Sessions.Count),
            ("currentConnections", _metrics.CurrentConnections));
    }

    private async Task DrainClientsBestEffortAsync(CancellationToken ct)
    {
        var tasks = _clientTasks.Values.ToArray();
        if (tasks.Length == 0)
            return;

        // Evita Task.WhenAll() falhar por exceções e mantém tudo "observed".
        var swallow = new Task[tasks.Length];
        for (var i = 0; i < tasks.Length; i++)
        {
            var t = tasks[i];
            swallow[i] = t.ContinueWith(
                _ => { },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        var all = Task.WhenAll(swallow);
        var timeout = Task.Delay(_clientDrainTimeout, ct);

        var completed = await Task.WhenAny(all, timeout).ConfigureAwait(false);
        if (completed != all)
        {
            LogWarn("server_stop_timeout",
                ("timeoutMs", (long)_clientDrainTimeout.TotalMilliseconds),
                ("remaining", _clientTasks.Count));
        }
        else
        {
            await AwaitQuietlyAsync(all, ct).ConfigureAwait(false);
        }
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        if (_listener is null)
            throw new InvalidOperationException("Listener não iniciado.");

        while (!ct.IsCancellationRequested)
        {
            TcpClient tcp;

            try
            {
                tcp = await _listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (SocketException) when (ct.IsCancellationRequested)
            {
                break;
            }

            if (_clientTasks.Count >= MaxTrackedClientTasks)
            {
                LogWarn("client_rejected",
                    ("reason", "too_many_tracked_tasks"),
                    ("limit", MaxTrackedClientTasks));
                try { tcp.Close(); } catch { }
                continue;
            }

            var session = Sessions.Register(tcp);
            session.State = SessionState.Connected;

            _metrics.OnAccept();

            LogInfo("client_connect",
                ("sessionId", session.SessionId),
                ("remote", session.RemoteEndPoint?.ToString() ?? "null"),
                ("current", _metrics.CurrentConnections));

            var task = HandleClientAsync(tcp, session, ct);
            _clientTasks[session.SessionId] = task;

            _ = task.ContinueWith(
                _ => _clientTasks.TryRemove(session.SessionId, out Task? _),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    private async Task HandleClientAsync(TcpClient tcp, Session session, CancellationToken serverCt)
    {
        string? disconnectEvt = null;
        DisconnectReason? disconnectReason = null;

        await using var conn = new Connection(
            tcp,
            new RateLimiter(maxMsgsPerSec: 60, maxBytesPerSec: 64 * 1024));

        try
        {
            // 1) Handshake obrigatório com timeout
            Handshake hs;
            try
            {
                using var hsCts = CancellationTokenSource.CreateLinkedTokenSource(serverCt);
                hsCts.CancelAfter(_handshakeTimeout);

                var (hsEnv, hsBody) = await conn.ReadAsync(hsCts.Token).ConfigureAwait(false);
                _metrics.IncMessagesIn();

                if (hsEnv.MessageType != MessageType.Handshake)
                    throw new BadHandshakeException("primeiro pacote não é Handshake");

                hs = Handshake.Read(hsBody);
            }
            catch (OperationCanceledException) when (!serverCt.IsCancellationRequested)
            {
                _metrics.IncParseError();
                await CloseAsync(conn, session, DisconnectReason.BadHandshake, "handshake_timeout").ConfigureAwait(false);
                disconnectEvt = "client_disconnect";
                disconnectReason = DisconnectReason.BadHandshake;
                return;
            }
            catch (BadHandshakeException)
            {
                _metrics.IncParseError();
                await CloseAsync(conn, session, DisconnectReason.BadHandshake, "bad_handshake").ConfigureAwait(false);
                disconnectEvt = "client_disconnect";
                disconnectReason = DisconnectReason.BadHandshake;
                return;
            }
            catch (InvalidOperationException)
            {
                _metrics.IncParseError();
                await CloseAsync(conn, session, DisconnectReason.BadHandshake, "bad_handshake").ConfigureAwait(false);
                disconnectEvt = "client_disconnect";
                disconnectReason = DisconnectReason.BadHandshake;
                return;
            }

            if (hs.Stage != HandshakeStage.Request || hs.Version != ProtocolVersion.V0)
            {
                _metrics.IncParseError();
                await CloseAsync(conn, session, DisconnectReason.BadHandshake, "bad_handshake").ConfigureAwait(false);
                disconnectEvt = "client_disconnect";
                disconnectReason = DisconnectReason.BadHandshake;
                return;
            }

            session.ProtocolVersion = hs.Version;
            session.HandshakeNonce = hs.Nonce;
            session.State = SessionState.Handshaken;

            var ack = new Handshake(hs.Version, hs.Nonce, HandshakeStage.Ack);
            if (!await TrySendAsync(conn, MessageType.Handshake, ack.ToBytes(), serverCt).ConfigureAwait(false))
            {
                disconnectEvt = "client_disconnect";
                disconnectReason = DisconnectReason.Unknown;
                return;
            }

            LogInfo("handshake_ok",
                ("sessionId", session.SessionId),
                ("v", hs.Version));

            // 2) Loop de mensagens por conexão
            while (!serverCt.IsCancellationRequested)
            {
                using var readCts = CancellationTokenSource.CreateLinkedTokenSource(serverCt);
                readCts.CancelAfter(_readIdleTimeout);

                var (env, body) = await conn.ReadAsync(readCts.Token).ConfigureAwait(false);
                _metrics.IncMessagesIn();

                if (env.MessageType == MessageType.Disconnect)
                {
                    var d = Disconnect.Read(body);

                    LogInfo("client_disconnect_request",
                        ("sessionId", session.SessionId),
                        ("reason", d.Reason));

                    await CloseAsync(conn, session, DisconnectReason.ClientClosed, "client_closed").ConfigureAwait(false);
                    disconnectEvt = "client_disconnect";
                    disconnectReason = DisconnectReason.ClientClosed;
                    return;
                }

                await _router.DispatchAsync(conn, env, body, serverCt).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (!serverCt.IsCancellationRequested)
        {
            // Timeout de leitura (idle)
            await CloseAsync(conn, session, DisconnectReason.Unknown, "read_idle_timeout").ConfigureAwait(false);
            disconnectEvt = "client_disconnect";
            disconnectReason = DisconnectReason.Unknown;
        }
        catch (RateLimitExceededException)
        {
            await CloseAsync(conn, session, DisconnectReason.RateLimit, "rate_limit").ConfigureAwait(false);
            disconnectEvt = "client_disconnect";
            disconnectReason = DisconnectReason.RateLimit;
        }
        catch (InvalidOperationException ex)
        {
            _metrics.IncParseError();
            LogWarn("protocol_error",
                ("sessionId", session.SessionId),
                ("msg", ex.Message));

            await CloseAsync(conn, session, DisconnectReason.ProtocolError, "protocol_error").ConfigureAwait(false);
            disconnectEvt = "client_disconnect";
            disconnectReason = DisconnectReason.ProtocolError;
        }
        catch (IOException ex)
        {
            _metrics.IncIoError();
            LogWarn("io_error",
                ("sessionId", session.SessionId),
                ("msg", ex.Message));

            disconnectEvt ??= "client_disconnect";
            disconnectReason ??= DisconnectReason.Unknown;
        }
        catch (OperationCanceledException) when (serverCt.IsCancellationRequested)
        {
            // shutdown
            await CloseAsync(conn, session, DisconnectReason.ServerShutdown, null).ConfigureAwait(false);
            disconnectEvt ??= "client_disconnect";
            disconnectReason ??= DisconnectReason.ServerShutdown;
        }
        catch (Exception ex)
        {
            _metrics.IncUnhandledError();
            LogError("unhandled_error", ex,
                ("sessionId", session.SessionId));

            disconnectEvt ??= "client_disconnect";
            disconnectReason ??= DisconnectReason.Unknown;
        }
        finally
        {
            session.State = SessionState.Closed;
            Sessions.TryRemove(session.SessionId, out _);

            _metrics.OnDisconnect();

            if (disconnectEvt is not null)
            {
                LogInfo(disconnectEvt,
                    ("sessionId", session.SessionId),
                    ("reason", disconnectReason?.ToString() ?? "null"),
                    ("current", _metrics.CurrentConnections));
            }
        }
    }

    private async Task CloseAsync(Connection conn, Session session, DisconnectReason reason, string? evt)
    {
        session.State = SessionState.Closing;

        if (!string.IsNullOrWhiteSpace(evt))
            LogInfo(evt, ("sessionId", session.SessionId));

        // Contabiliza como "tentativa" de envio (best-effort). Para contagem exata, precisaria instrumentar o Connection.
        _metrics.IncMessagesOut();
        await conn.SendDisconnectAndCloseAsync(reason).ConfigureAwait(false);

        session.State = SessionState.Closed;
    }

    private async Task<bool> TrySendAsync(Connection conn, MessageType type, byte[] body, CancellationToken serverCt)
    {
        using var sendCts = CancellationTokenSource.CreateLinkedTokenSource(serverCt);
        sendCts.CancelAfter(_writeTimeout);

        try
        {
            _metrics.IncMessagesOut();
            await conn.SendAsync(type, body, sendCts.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (!serverCt.IsCancellationRequested)
        {
            LogWarn("write_timeout",
                ("type", type),
                ("timeoutMs", (long)_writeTimeout.TotalMilliseconds));
            return false;
        }
    }

    private static async Task AwaitQuietlyAsync(Task task, CancellationToken ct)
    {
        try
        {
            await task.WaitAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            // intentionally ignored
        }
    }

    private static IPAddress ResolveIp(string host)
    {
        if (IPAddress.TryParse(host, out var ip))
            return ip;

        // fallback: resolve DNS (prefer IPv4)
        var addrs = Dns.GetHostAddresses(host);
        var v4 = addrs.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
        if (v4 is not null) return v4;

        var any = addrs.FirstOrDefault();
        if (any is not null) return any;

        throw new InvalidOperationException($"Não foi possível resolver host '{host}'.");
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        try { _cts?.Dispose(); } catch { }
    }

    private void LogInfo(string evt, params (string Key, object? Value)[] fields)
        => _log(FormatLog("INFO", evt, fields));

    private void LogWarn(string evt, params (string Key, object? Value)[] fields)
        => _log(FormatLog("WARN", evt, fields));

    private void LogError(string evt, Exception ex, params (string Key, object? Value)[] fields)
    {
        var merged = new (string Key, object? Value)[fields.Length + 2];
        for (var i = 0; i < fields.Length; i++) merged[i] = fields[i];
        merged[fields.Length] = ("exType", ex.GetType().Name);
        merged[fields.Length + 1] = ("exMsg", ex.Message);
        _log(FormatLog("ERROR", evt, merged));
    }

    private static string FormatLog(string level, string evt, (string Key, object? Value)[] fields)
    {
        var sb = new StringBuilder(256);
        sb.Append("ts=").Append(DateTimeOffset.UtcNow.ToString("O"))
          .Append(" level=").Append(level)
          .Append(" evt=").Append(evt);

        foreach (var (k, v) in fields)
        {
            if (string.IsNullOrWhiteSpace(k)) continue;
            sb.Append(' ').Append(SanitizeKey(k)).Append('=');
            AppendValue(sb, v);
        }

        return sb.ToString();
    }

    private static string SanitizeKey(string key)
    {
        var sb = new StringBuilder(key.Length);
        foreach (var ch in key)
        {
            if (char.IsLetterOrDigit(ch) || ch == '_' || ch == '-') sb.Append(ch);
            else sb.Append('_');
        }
        return sb.ToString();
    }

    private static void AppendValue(StringBuilder sb, object? value)
    {
        if (value is null) { sb.Append("null"); return; }

        if (value is string s)
        {
            if (s.Length == 0) { sb.Append("\"\""); return; }
            var needsQuote = s.IndexOfAny(new[] { ' ', '\t', '\r', '\n', '"' }) >= 0;
            if (!needsQuote) { sb.Append(s); return; }
            sb.Append('"').Append(s.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"');
            return;
        }

        sb.Append(value);
    }

    private sealed class BadHandshakeException : Exception
    {
        public BadHandshakeException(string message) : base(message) { }
    }
}
