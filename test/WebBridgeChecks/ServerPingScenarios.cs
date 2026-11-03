using System.Net;
using System.Net.Sockets;
using System.Text;
using MCUGLauncher.Services;

internal static class ServerPingScenarios
{
    public static async Task Run()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var serve = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync(deadline.Token);
            using var stream = client.GetStream();
            await ReadPacket(stream, deadline.Token); // handshake
            await ReadPacket(stream, deadline.Token); // status request
            var json = Encoding.UTF8.GetBytes("""{"description":{"text":"§aHello","extra":[{"text":" world"}]},"version":{"name":"Fixture"},"players":{"online":3,"max":12}}""");
            using var status = new MemoryStream();
            status.WriteByte(0);
            WriteVar(status, json.Length);
            status.Write(json);
            await Packet(stream, status.ToArray(), deadline.Token);
            var ping = await ReadPacket(stream, deadline.Token);
            if (ping.Length != 9 || ping[0] != 1)
                throw new Exception("Core must request a real ping response");
            await Packet(stream, ping, deadline.Token);
        });
        var result = await ServerPingService.PingAsync("127.0.0.1:" + port, deadline.Token);
        await serve;
        if (!result.Online || result.OnlinePlayers != 3 || result.MaxPlayers != 12
            || result.Motd != "Hello world" || result.Version != "Fixture" || result.Ping < 0)
            throw new Exception("server adapter lost Core status fields or nested MOTD");
        Console.WriteLine("PASS server adapter uses Core SLP and real pong, preserving nested MOTD and counts");

        foreach (var address in new[] { "", "localhost:99999", "[invalid]" })
        {
            var failure = await ServerPingService.PingAsync(address, deadline.Token);
            if (failure.Online || failure.Error.Length == 0)
                throw new Exception("invalid endpoint escaped adapter");
        }
        var malformedServer = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync(deadline.Token);
            using var stream = client.GetStream();
            await ReadPacket(stream, deadline.Token);
            await ReadPacket(stream, deadline.Token);
            await Packet(stream, new byte[] { 2 }, deadline.Token);
        });
        var malformed = await ServerPingService.PingAsync("127.0.0.1:" + port, deadline.Token);
        await malformedServer;
        if (malformed.Online || malformed.Error.Length == 0)
            throw new Exception("malformed server response accepted");
        Console.WriteLine("PASS malformed server packet is validated by Core and reported offline");
        listener.Stop();
        var offline = await ServerPingService.PingAsync("127.0.0.1:" + port, deadline.Token);
        if (offline.Online || offline.Error.Length == 0)
            throw new Exception("closed port accepted");
        Console.WriteLine("PASS invalid addresses and closed ports return observable offline state");
    }

    static async Task<byte[]> ReadPacket(Stream stream, CancellationToken token)
    {
        var length = 0;
        for (var shift = 0; shift < 35; shift += 7)
        {
            var one = new byte[1];
            await stream.ReadExactlyAsync(one, token);
            length |= (one[0] & 127) << shift;
            if ((one[0] & 128) != 0)
                continue;
            if (length is < 1 or > 65536)
                throw new IOException("fixture packet too large");
            var data = new byte[length];
            await stream.ReadExactlyAsync(data, token);
            return data;
        }
        throw new IOException("fixture varint too long");
    }
    static async Task Packet(Stream stream, byte[] payload, CancellationToken token)
    {
        using var output = new MemoryStream();
        WriteVar(output, payload.Length);
        output.Write(payload);
        await stream.WriteAsync(output.ToArray(), token);
    }
    static void WriteVar(Stream stream, int value)
    {
        do
        {
            var part = value & 127;
            value >>= 7;
            stream.WriteByte((byte)(value == 0 ? part : part | 128));
        } while (value != 0);
    }
}
