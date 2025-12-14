namespace FireAndSteel.Networking.Net;

public sealed class MessageRouter
{
    private readonly Dictionary<MessageType, Func<Connection, EnvelopeV1, byte[], CancellationToken, Task>> _handlers = new();

    public void Register(MessageType type, Func<Connection, EnvelopeV1, byte[], CancellationToken, Task> handler)
        => _handlers[type] = handler;

    public Task DispatchAsync(Connection conn, EnvelopeV1 env, byte[] body, CancellationToken ct)
    {
        if (_handlers.TryGetValue(env.MessageType, out var handler))
            return handler(conn, env, body, ct);

        // default: ignorar desconhecido (por enquanto)
        return Task.CompletedTask;
    }
}
