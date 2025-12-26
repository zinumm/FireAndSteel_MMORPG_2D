namespace FireAndSteel.Networking.Net;

internal sealed class ServerMetrics
{
    private long _currentConnections;
    private long _totalConnections;
    private long _totalDisconnects;

    private long _messagesIn;
    private long _messagesOut;

    private long _ioErrors;
    private long _parseErrors;
    private long _unhandledErrors;

    public long CurrentConnections => Interlocked.Read(ref _currentConnections);
    public long TotalConnections => Interlocked.Read(ref _totalConnections);
    public long TotalDisconnects => Interlocked.Read(ref _totalDisconnects);

    public long MessagesIn => Interlocked.Read(ref _messagesIn);
    public long MessagesOut => Interlocked.Read(ref _messagesOut);

    public long IoErrors => Interlocked.Read(ref _ioErrors);
    public long ParseErrors => Interlocked.Read(ref _parseErrors);
    public long UnhandledErrors => Interlocked.Read(ref _unhandledErrors);

    public void OnAccept()
    {
        Interlocked.Increment(ref _totalConnections);
        Interlocked.Increment(ref _currentConnections);
    }

    public void OnDisconnect()
    {
        Interlocked.Increment(ref _totalDisconnects);
        Interlocked.Decrement(ref _currentConnections);
    }

    public void IncMessagesIn() => Interlocked.Increment(ref _messagesIn);
    public void IncMessagesOut() => Interlocked.Increment(ref _messagesOut);

    public void IncIoError() => Interlocked.Increment(ref _ioErrors);
    public void IncParseError() => Interlocked.Increment(ref _parseErrors);
    public void IncUnhandledError() => Interlocked.Increment(ref _unhandledErrors);

}
