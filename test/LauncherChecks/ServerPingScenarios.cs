using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using MCUGLauncher.Services;

internal static class ServerPingScenarios
{
    public static async Task Run()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var endpoint = (IPEndPoint)listener.LocalEndpoint;
            var server = Serve(listener);
            var result = await ServerPingService.PingAsync($"127.0.0.1:{endpoint.Port}", CancellationToken.None);
            var handshake = await server.WaitAsync(TimeSpan.FromSeconds(5));
            var offset = 1;
            var protocol = ReadVarInt(handshake, ref offset);
            Check(protocol == -1, "server status uses protocol auto-negotiation");
            Check(result.Online && result.OnlinePlayers == 3 && result.MaxPlayers == 20
                && result.Version == "Fixture 1.21" && result.Motd == "A server",
                "server ping validates pong and maps Core status into UI result");
        }
        finally
        {
            listener.Stop();
        }

        var invalid = await ServerPingService.PingAsync("not a server address", CancellationToken.None);
        Check(!invalid.Online && invalid.Error.Length > 0, "server ping reports invalid addresses as failures");
    }

    static async Task<byte[]> Serve(TcpListener listener)
    {
        using var client = await listener.AcceptTcpClientAsync();
        var stream = client.GetStream();
        var handshake = await ReadPacket(stream);
        await ReadPacket(stream);
        const string status = """{"version":{"name":"Fixture 1.21"},"players":{"online":3,"max":20},"description":{"text":"A§a","extra":[{"text":" server"}]}}""";
        using var response = new MemoryStream();
        WriteVarInt(response, 0);
        var json = Encoding.UTF8.GetBytes(status);
        WriteVarInt(response, json.Length);
        response.Write(json);
        await WritePacket(stream, response.ToArray());
        var ping = await ReadPacket(stream);
        await WritePacket(stream, ping);
        return handshake;
    }

    static async Task<byte[]> ReadPacket(NetworkStream stream)
    {
        var length = ReadVarInt(stream);
        if (length is < 1 or > 2 * 1024 * 1024)
            throw new IOException("Unexpected packet size.");
        var payload = new byte[length];
        await stream.ReadExactlyAsync(payload);
        return payload;
    }

    static int ReadVarInt(Stream stream)
    {
        var value = 0;
        for (var shift = 0; shift < 35; shift += 7)
        {
            var next = stream.ReadByte();
            if (next < 0)
                throw new EndOfStreamException();
            value |= (next & 0x7F) << shift;
            if ((next & 0x80) == 0)
                return value;
        }
        throw new IOException("Unexpected VarInt size.");
    }

    static int ReadVarInt(byte[] payload, ref int offset)
    {
        var value = 0;
        for (var shift = 0; shift < 35; shift += 7)
        {
            if (offset >= payload.Length)
                throw new EndOfStreamException();
            var next = payload[offset++];
            value |= (next & 0x7F) << shift;
            if ((next & 0x80) == 0)
                return value;
        }
        throw new IOException("Unexpected VarInt size.");
    }

    static async Task WritePacket(NetworkStream stream, byte[] payload)
    {
        using var packet = new MemoryStream();
        WriteVarInt(packet, payload.Length);
        packet.Write(payload);
        await stream.WriteAsync(packet.ToArray(), CancellationToken.None);
    }

    static void WriteVarInt(Stream stream, int value)
    {
        var unsigned = (uint)value;
        do
        {
            var next = (byte)(unsigned & 0x7F);
            unsigned >>= 7;
            if (unsigned > 0)
                next |= 0x80;
            stream.WriteByte(next);
        } while (unsigned > 0);
    }

    static void Check(bool success, string label)
    {
        if (!success)
            throw new Exception(label);
        Console.WriteLine("PASS " + label);
    }
}
