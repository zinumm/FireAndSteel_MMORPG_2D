using System.Net;
using FireAndSteel.Networking.Net;

namespace FireAndSteel.Server.Net;

public enum SessionState
{
    Connected = 0,
    Handshaken = 1,
    Closing = 2,
    Closed = 3,
}

public sealed class Session
{
    public long SessionId { get; }
    public EndPoint? RemoteEndPoint { get; }

    public ProtocolVersion ProtocolVersion { get; internal set; } = ProtocolVersion.V0;
    public uint HandshakeNonce { get; internal set; }
    public SessionState State { get; internal set; } = SessionState.Connected;

    internal Session(long sessionId, EndPoint? remote)
    {
        SessionId = sessionId;
        RemoteEndPoint = remote;
    }

    public override string ToString() => $"SessionId={SessionId} Remote={RemoteEndPoint}";
}
