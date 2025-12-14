using System.Net.Sockets;
using FireAndSteel.Networking.Net;
using FireAndSteel.Server.Net;
using Xunit;

namespace FireAndSteel.Tests;

public sealed class ServerHandshakeTests
{
    [Fact]
    public async Task Invalid_first_message_disconnects_with_BadHandshake()
    {
        var router = new MessageRouter();
        router.Register(MessageType.Ping, async (conn, env, body, ct) =>
        {
            await conn.SendAsync(MessageType.Pong, Array.Empty<byte>(), ct);
        });

        await using var server = new ServerHost(
            host: "127.0.0.1",
            port: 0,
            router: router,
            logger: _ => { },
            handshakeTimeout: TimeSpan.FromSeconds(1));

        server.Start();

        var port = server.BoundEndPoint!.Port;

        using var tcp = new TcpClient();
        await tcp.ConnectAsync("127.0.0.1", port);

        await using var conn = new Connection(tcp);

        // envia Ping como primeira mensagem (inválido)
        await conn.SendAsync(MessageType.Ping, Array.Empty<byte>(), CancellationToken.None);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var (env, body) = await conn.ReadAsync(cts.Token);

        Assert.Equal(MessageType.Disconnect, env.MessageType);
        var disc = Disconnect.Read(body);
        Assert.Equal(DisconnectReason.BadHandshake, disc.Reason);
    }

    [Fact]
    public async Task Handshake_timeout_disconnects_with_BadHandshake()
    {
        var router = new MessageRouter();

        await using var server = new ServerHost(
            host: "127.0.0.1",
            port: 0,
            router: router,
            logger: _ => { },
            handshakeTimeout: TimeSpan.FromMilliseconds(200));

        server.Start();

        var port = server.BoundEndPoint!.Port;

        using var tcp = new TcpClient();
        await tcp.ConnectAsync("127.0.0.1", port);

        await using var conn = new Connection(tcp);

        // não envia nada -> server deve expirar handshake e desconectar
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var (env, body) = await conn.ReadAsync(cts.Token);

        Assert.Equal(MessageType.Disconnect, env.MessageType);
        var disc = Disconnect.Read(body);
        Assert.Equal(DisconnectReason.BadHandshake, disc.Reason);
    }
}
