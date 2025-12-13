namespace FireAndSteel.Networking.Net;

// Token bucket simples: limita msgs/s e bytes/s
public sealed class RateLimiter
{
    private readonly int _maxMsgsPerSec;
    private readonly int _maxBytesPerSec;

    private double _msgTokens;
    private double _byteTokens;

    private long _lastTicks;

    public RateLimiter(int maxMsgsPerSec, int maxBytesPerSec)
    {
        _maxMsgsPerSec = Math.Max(1, maxMsgsPerSec);
        _maxBytesPerSec = Math.Max(256, maxBytesPerSec);

        _msgTokens = _maxMsgsPerSec;
        _byteTokens = _maxBytesPerSec;
        _lastTicks = DateTime.UtcNow.Ticks;
    }

    public bool TryConsume(int messages, int bytes)
    {
        Refill();

        if (_msgTokens < messages) return false;
        if (_byteTokens < bytes) return false;

        _msgTokens -= messages;
        _byteTokens -= bytes;
        return true;
    }

    private void Refill()
    {
        var now = DateTime.UtcNow.Ticks;
        var elapsedSec = (now - _lastTicks) / (double)TimeSpan.TicksPerSecond;
        if (elapsedSec <= 0) return;

        _msgTokens = Math.Min(_maxMsgsPerSec, _msgTokens + elapsedSec * _maxMsgsPerSec);
        _byteTokens = Math.Min(_maxBytesPerSec, _byteTokens + elapsedSec * _maxBytesPerSec);
        _lastTicks = now;
    }
}
