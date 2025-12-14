using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using FireAndSteel.Networking.Net;

namespace FireAndSteel.Server.Net;

public sealed class ServerHost : IAsyncDisposable
{
    private readonly string _host;
    private readonly int _port;
    private readonly TimeSpan _handshakeTimeout;
    private readonly Action<string> _log;
    private readonly MessageRouter _router;

    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptLoopTask;

    private readonly ConcurrentDictionary<long, Task> _clientTasks = new();

    public SessionManager Sessions { get; } = new();

    public IPEndPoint? BoundEndPoint { get; private set; }

    public ServerHost(
        string host,
        int port,
        MessageRouter router,
        Action<string>? logger = null,
        TimeSpan? handshakeTimeout = null)
    {
        _host = host;
        _port = port;
        _router = router ?? throw new ArgumentNullException(nameof(router));
        _log = logger ?? (_ => { });
        _handshakeTimeout = handshakeTimeout ?? TimeSpan.FromSeconds(2);
    }

    public void Start(CancellationToken ct = default)
    {
        if (_listener is not null)
            throw new InvalidOperationException("ServerHost já iniciado.");

        var ip = ResolveIp(_host);
        _listener = new TcpListener(ip, _port);
        _listener.Start();

        BoundEndPoint = (IPEndPoint)_listener.LocalEndpoint;
        _log($"[Server] Listening on {BoundEndPoint.Address}:{BoundEndPoint.Port}");

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _acceptLoopTask = AcceptLoopAsync(_cts.Token);
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_cts is null)
            return;

        try { _cts.Cancel(); } catch { }

        try { _listener?.Stop(); } catch { }

        if (_acceptLoopTask is not null)
        {
            try { await _acceptLoopTask; } catch { }
        }

        // aguarda handlers em andamento
        if (_clientTasks.Count > 0)
        {
            try { await Task.WhenAll(_clientTasks.Values); } catch { }
        }
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        if (_listener is null)
            throw new InvalidOperationException("Listener não iniciado.");

        while (!ct.IsCancellationRequested)
        {
            TcpClient? tcp = null;
            try
            {
                tcp = await _listener.AcceptTcpClientAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }

            var session = Sessions.Register(tcp);
            session.State = SessionState.Connected;

            _log($"[Server] Connect {session.RemoteEndPoint} (sessionId={session.SessionId})");

            var task = HandleClientAsync(tcp, session, ct);
            _clientTasks[session.SessionId] = task;

            _ = task.ContinueWith(_ =>
            {
                _clientTasks.TryRemove(session.SessionId, out _);
            }, CancellationToken.None);
        }
    }

    private async Task HandleClientAsync(TcpClient tcp, Session session, CancellationToken serverCt)
    {
        string? disconnectLog = null;

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

                var (hsEnv, hsBody) = await conn.ReadAsync(hsCts.Token);
                if (hsEnv.MessageType != MessageType.Handshake)
                    throw new BadHandshakeException("primeiro pacote não é Handshake");

                hs = Handshake.Read(hsBody);
            }
            catch (OperationCanceledException) when (!serverCt.IsCancellationRequested)
            {
                await CloseAsync(conn, session, DisconnectReason.BadHandshake, "Handshake timeout.");
                disconnectLog = $"[Server] Disconnect (sessionId={session.SessionId} reason=BadHandshake)";
                return;
            }
            catch (BadHandshakeException ex)
            {
                await CloseAsync(conn, session, DisconnectReason.BadHandshake, $"Bad handshake: {ex.Message}");
                disconnectLog = $"[Server] Disconnect (sessionId={session.SessionId} reason=BadHandshake)";
                return;
            }
            catch (InvalidOperationException ex)
            {
                await CloseAsync(conn, session, DisconnectReason.BadHandshake, $"Bad handshake: {ex.Message}");
                disconnectLog = $"[Server] Disconnect (sessionId={session.SessionId} reason=BadHandshake)";
                return;
            }

            if (hs.Stage != HandshakeStage.Request || hs.Version != ProtocolVersion.V0)
            {
                await CloseAsync(conn, session, DisconnectReason.BadHandshake,
                    $"Bad handshake: stage={hs.Stage} version={hs.Version}");
                disconnectLog = $"[Server] Disconnect (sessionId={session.SessionId} reason=BadHandshake)";
                return;
            }

            session.ProtocolVersion = hs.Version;
            session.HandshakeNonce = hs.Nonce;
            session.State = SessionState.Handshaken;

            var ack = new Handshake(hs.Version, hs.Nonce, HandshakeStage.Ack);
            await conn.SendAsync(MessageType.Handshake, ack.ToBytes(), serverCt);

            _log($"[Server] Handshake OK (sessionId={session.SessionId} v={hs.Version})");

            // 2) Loop de mensagens por conexão
            while (!serverCt.IsCancellationRequested)
            {
                var (env, body) = await conn.ReadAsync(serverCt);

                if (env.MessageType == MessageType.Disconnect)
                {
                    var d = Disconnect.Read(body);
                    _log($"[Server] Client requested disconnect (sessionId={session.SessionId} reason={d.Reason})");
                    await CloseAsync(conn, session, DisconnectReason.ClientClosed, null);
                    disconnectLog = $"[Server] Disconnect (sessionId={session.SessionId} reason=ClientClosed)";
                    return;
                }

                await _router.DispatchAsync(conn, env, body, serverCt);
            }
        }
        catch (RateLimitExceededException)
        {
            await CloseAsync(conn, session, DisconnectReason.RateLimit, "Rate limit excedido.");
            disconnectLog = $"[Server] Disconnect (sessionId={session.SessionId} reason=RateLimit)";
        }
        catch (InvalidOperationException ex)
        {
            await CloseAsync(conn, session, DisconnectReason.ProtocolError, $"Protocol error: {ex.Message}");
            disconnectLog = $"[Server] Disconnect (sessionId={session.SessionId} reason=ProtocolError)";
        }
        catch (IOException)
        {
            disconnectLog ??= $"[Server] Disconnect (sessionId={session.SessionId} io)";
        }
        catch (OperationCanceledException) when (serverCt.IsCancellationRequested)
        {
            // shutdown
            await CloseAsync(conn, session, DisconnectReason.ServerShutdown, null);
            disconnectLog ??= $"[Server] Disconnect (sessionId={session.SessionId} reason=ServerShutdown)";
        }
        catch (Exception ex)
        {
            disconnectLog ??= $"[Server] Disconnect (sessionId={session.SessionId} err={ex.GetType().Name})";
            _log($"[Server] Conn drop (sessionId={session.SessionId}): {ex.Message}");
        }
        finally
        {
            session.State = SessionState.Closed;
            Sessions.TryRemove(session.SessionId, out _);

            if (disconnectLog is not null)
                _log(disconnectLog);
        }
    }

    private async Task CloseAsync(Connection conn, Session session, DisconnectReason reason, string? logLine)
    {
        session.State = SessionState.Closing;

        if (!string.IsNullOrWhiteSpace(logLine))
            _log($"[Server] {logLine} (sessionId={session.SessionId})");

        await conn.SendDisconnectAndCloseAsync(reason);
        session.State = SessionState.Closed;
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
        await StopAsync();
        try { _cts?.Dispose(); } catch { }
    }

    private sealed class BadHandshakeException : Exception
    {
        public BadHandshakeException(string message) : base(message) { }
    }
}
