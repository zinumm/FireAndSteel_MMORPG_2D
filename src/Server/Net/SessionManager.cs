using System.Collections.Concurrent;
using System.Net.Sockets;

namespace FireAndSteel.Server.Net;

public sealed class SessionManager
{
    private long _nextId = 0;
    private readonly ConcurrentDictionary<long, Session> _sessions = new();

    public Session Register(TcpClient tcp)
    {
        var id = Interlocked.Increment(ref _nextId);
        var session = new Session(id, tcp.Client.RemoteEndPoint);
        _sessions[id] = session;
        return session;
    }

    public bool TryRemove(long sessionId, out Session? removed)
        => _sessions.TryRemove(sessionId, out removed);

    public bool TryGet(long sessionId, out Session? session)
        => _sessions.TryGetValue(sessionId, out session);

    public int Count => _sessions.Count;

    public IReadOnlyCollection<Session> Snapshot()
        => _sessions.Values.ToArray();
}
