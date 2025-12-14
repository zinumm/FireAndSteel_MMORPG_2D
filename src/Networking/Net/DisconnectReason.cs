namespace FireAndSteel.Networking.Net;

public enum DisconnectReason : ushort
{
    Unknown = 0,
    RateLimit = 1,
    ProtocolError = 2,
    BadHandshake = 3,
    ServerShutdown = 4,
    ClientClosed = 5,
}
