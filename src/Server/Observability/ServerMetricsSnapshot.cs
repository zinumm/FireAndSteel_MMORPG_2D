using FireAndSteel.Networking.Net;

namespace FireAndSteel.Server.Observability;

internal readonly record struct ServerMetricsSnapshot(
    long currentConnections,
    long totalConnections,
    long totalDisconnects,
    long messagesIn,
    long messagesOut,
    long ioErrors,
    long parseErrors,
    long unhandledErrors)
{
    public static ServerMetricsSnapshot From(ServerMetrics metrics)
    {
        if (metrics is null)
            throw new ArgumentNullException(nameof(metrics));

        return new ServerMetricsSnapshot(
            currentConnections: metrics.CurrentConnections,
            totalConnections: metrics.TotalConnections,
            totalDisconnects: metrics.TotalDisconnects,
            messagesIn: metrics.MessagesIn,
            messagesOut: metrics.MessagesOut,
            ioErrors: metrics.IoErrors,
            parseErrors: metrics.ParseErrors,
            unhandledErrors: metrics.UnhandledErrors);
    }
}
