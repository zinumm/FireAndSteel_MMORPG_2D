namespace FireAndSteel.Networking.Net;

public enum MessageType : ushort
{
    Ping = 1,
    Pong = 2,

    Handshake = 3,
    Disconnect = 4,
}
