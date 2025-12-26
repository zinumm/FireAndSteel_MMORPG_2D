using FireAndSteel.Networking.Net;
using FireAndSteel.Server.Net;
using Xunit;

namespace FireAndSteel.Server.Tests;

public sealed class ServerHostLifecycleTests
{
    [Fact]
    public async Task StopAsync_IsIdempotent_LogsStopBeginEndOnce()
    {
        // Isola de env vars do ambiente/CI.
        Environment.SetEnvironmentVariable("FNS_SERVER_METRICS_SNAPSHOT_ENABLED", "false");

        var log = new InMemoryLogger();
        var router = new MessageRouter();

        await using var server = new ServerHost(
            host: "127.0.0.1",
            port: 0,
            router: router,
            logger: log.Sink);

        server.Start(CancellationToken.None);

        await server.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2));
        await server.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(1, log.CountContaining("evt=server_stop_begin"));
        Assert.Equal(1, log.CountContaining("evt=server_stop_end"));
    }

    [Fact]
    public async Task StartStop_DoesNotThrow_AndCompletesQuickly()
    {
        Environment.SetEnvironmentVariable("FNS_SERVER_METRICS_SNAPSHOT_ENABLED", "false");

        var log = new InMemoryLogger();
        var router = new MessageRouter();

        await using var server = new ServerHost(
            host: "127.0.0.1",
            port: 0,
            router: router,
            logger: log.Sink);

        server.Start(CancellationToken.None);
        await server.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2));
    }
}
