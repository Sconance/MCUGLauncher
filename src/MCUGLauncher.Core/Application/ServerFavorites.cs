using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace BlockLaunch;

/// <summary>
/// 表示 服务器Favorite 数据。
/// </summary>
public record ServerFavorite(string Id, string Name, string Address, string Profile);

/// <summary>
/// 表示 服务器Endpoint 数据。
/// </summary>
public record ServerEndpoint(string Host, int Port)
{
    /// <summary>
    /// 解析。
    /// </summary>
    public static ServerEndpoint Parse(string address)
    {
        address = address.Trim();
        if (address.Length == 0 || address.Length > 300 || address.Any(char.IsWhiteSpace) || address.IndexOfAny(['/', '\\', '@', '?', '#']) >= 0)
            throw new IOException("请输入服务器域名或 IP，可附带端口。");
        string host;
        var port = 25565;
        if (address.StartsWith('['))
        {
            var end = address.IndexOf(']');
            if (end < 0)
                throw new IOException("IPv6 地址格式无效。");
            host = address[1..end];
            if (!IPAddress.TryParse(host, out var ip) || ip.AddressFamily != AddressFamily.InterNetworkV6)
                throw new IOException("IPv6 地址无效。");
            var suffix = address[(end + 1)..];
            if (suffix.Length > 0 && (!suffix.StartsWith(':') || !int.TryParse(suffix[1..], out port)))
                throw new IOException("端口无效。");
        }
        else if (address.Count(c => c == ':') > 1)
        {
            if (!IPAddress.TryParse(address, out var ip) || ip.AddressFamily != AddressFamily.InterNetworkV6)
                throw new IOException("IPv6 地址无效。");
            host = address;
        }
        else
        {
            var parts = address.Split(':');
            host = parts[0];
            if (parts.Length > 1 && !int.TryParse(parts[1], out port))
                throw new IOException("端口无效。");
            if (Uri.CheckHostName(host) == UriHostNameType.Unknown)
                throw new IOException("服务器域名无效。");
        }

        if (port is < 1 or > 65535)
            throw new IOException("端口应为 1–65535。");
        return new(host, port);
    }

    /// <summary>
    /// 执行 ToString。
    /// </summary>
    public override string ToString() => (Host.Contains(':') ? "[" + Host + "]" : Host) + ":" + Port;
}

/// <summary>
/// 表示 服务器状态 数据。
/// </summary>
public record ServerStatus(string Description, string Version, int Online, int Maximum, long LatencyMs);

/// <summary>
/// 提供 服务器Favorites 相关功能。
/// </summary>
public static class ServerFavorites
{
    /// <summary>
    /// 查询。
    /// </summary>
    public static async Task<ServerStatus> Ping(string address, CancellationToken token = default)
    {
        var endpoint = ServerEndpoint.Parse(address);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        var ct = timeout.Token;
        using var client = new TcpClient();
        await client.ConnectAsync(endpoint.Host, endpoint.Port, ct);
        using var stream = client.GetStream();
        using var handshake = new MemoryStream();
        Var(handshake, 0);
        Var(handshake, -1);
        var host = Encoding.UTF8.GetBytes(endpoint.Host);
        Var(handshake, host.Length);
        handshake.Write(host);
        handshake.WriteByte((byte)(endpoint.Port >> 8));
        handshake.WriteByte((byte)endpoint.Port);
        Var(handshake, 1);
        await Packet(stream, handshake.ToArray(), ct);
        await Packet(stream, [0], ct);
        var data = await ReadPacket(stream, ct);
        using var payload = new MemoryStream(data);
        if (await ReadVar(payload, ct) != 0)
            throw new IOException("服务器状态响应无效。");
        var length = await ReadVar(payload, ct);
        if (length < 0 || length > payload.Length - payload.Position)
            throw new IOException("服务器状态长度无效。");
        var json = new byte[length];
        await payload.ReadExactlyAsync(json, ct);
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
        var root = doc.RootElement;
        var ping = new byte[9];
        ping[0] = 1;
        BinaryPrimitives.WriteInt64BigEndian(ping.AsSpan(1), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var watch = Stopwatch.StartNew();
        await Packet(stream, ping, ct);
        var pong = await ReadPacket(stream, ct);
        if (!pong.SequenceEqual(ping))
            throw new IOException("服务器 Ping 响应无效。");
        var players = root.TryGetProperty("players", out var p) ? p : default;
        return new(root.TryGetProperty("description", out var description) ? Description(description) : "", root.TryGetProperty("version", out var v) && v.TryGetProperty("name", out var name) ? name.GetString() ?? "未知" : "未知", players.ValueKind == JsonValueKind.Object && players.TryGetProperty("online", out var online) ? online.GetInt32() : 0, players.ValueKind == JsonValueKind.Object && players.TryGetProperty("max", out var maximum) ? maximum.GetInt32() : 0, watch.ElapsedMilliseconds);
    }

    static string Description(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? "",
        JsonValueKind.Array => string.Concat(value.EnumerateArray().Select(Description)),
        JsonValueKind.Object => (value.TryGetProperty("text", out var text) ? text.GetString() : "") + (value.TryGetProperty("extra", out var extra) ? Description(extra) : ""),
        _ => ""
    };

    static async Task Packet(Stream stream, byte[] data, CancellationToken token)
    {
        using var packet = new MemoryStream();
        Var(packet, data.Length);
        packet.Write(data);
        await stream.WriteAsync(packet.ToArray(), token);
    }

    static async Task<byte[]> ReadPacket(Stream stream, CancellationToken token)
    {
        var length = await ReadVar(stream, token);
        if (length is < 1 or > 2097152)
            throw new IOException("服务器响应包大小无效。");
        var data = new byte[length];
        await stream.ReadExactlyAsync(data, token);
        return data;
    }

    static void Var(Stream stream, int value)
    {
        var n = (uint)value;
        do
        {
            var b = (byte)(n & 127);
            n >>= 7;
            stream.WriteByte((byte)(n > 0 ? b | 128 : b));
        }
        while (n > 0);
    }

    static async Task<int> ReadVar(Stream stream, CancellationToken token)
    {
        var n = 0;
        var b = new byte[1];
        for (var i = 0; i < 5; i++)
        {
            await stream.ReadExactlyAsync(b, token);
            n |= (b[0] & 127) << (7 * i);
            if ((b[0] & 128) == 0)
                return n;
        }

        throw new IOException("VarInt 过长。");
    }
}
