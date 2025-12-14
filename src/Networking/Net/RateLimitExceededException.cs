namespace FireAndSteel.Networking.Net;

public sealed class RateLimitExceededException : Exception
{
    public int Messages { get; }
    public int Bytes { get; }

    public RateLimitExceededException(int messages, int bytes)
        : base("Rate limit excedido.")
    {
        Messages = messages;
        Bytes = bytes;
    }
}
