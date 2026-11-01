using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text;

namespace MCUGLauncher.Services;

/// <summary>
/// 表示 服务器状态结果 数据。
/// </summary>
public sealed record PingResult(bool Online, int OnlinePlayers, int MaxPlayers, int Ping, string Motd, string Version, string Error);

/// <summary>
/// Minecraft 服务器 SLP 查询。
/// </summary>
/// <remarks>
/// 直接使用 TCP/HTTP 读取服务器状态，不依赖游戏客户端。
/// </remarks>
public static class ServerPingService
{
    const int Protocol = 763;

    /// <summary>
    /// 查询服务器状态。
    /// </summary>
    /// <param name="address">服务器地址，可带端口</param>
    /// <param name="token">取消令牌</param>
    /// <returns>服务器状态查询结果</returns>
    /// <remarks>
    /// 返回在线人数、最大人数、延迟、MOTD 与版本；失败时 Online=false 并带 Error。
    /// </remarks>
    public static async Task<PingResult> PingAsync(string address, CancellationToken token)
    {
        var (host, port) = Split(address);
        if (host.Length == 0)
            return new PingResult(false, 0, 0, 0, "", "", "地址为空");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        var watch = Stopwatch.StartNew();
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port, timeout.Token);
            await using var stream = client.GetStream();

            var handshake = new MemoryStream();
            WriteVarInt(handshake, 0x00);
            WriteVarInt(handshake, Protocol);
            WriteString(handshake, host);
            WriteUInt16(handshake, (ushort)port);
            WriteVarInt(handshake, 1);
            await WritePacket(stream, handshake.ToArray(), timeout.Token);

            var request = new MemoryStream();
            WriteVarInt(request, 0x00);
            await WritePacket(stream, request.ToArray(), timeout.Token);

            var length = await ReadVarInt(stream, timeout.Token);
            if (length <= 0 || length > 2 * 1024 * 1024)
                return new PingResult(false, 0, 0, 0, "", "", "服务器返回了异常数据");
            var packet = await ReadExactly(stream, length, timeout.Token);
            using var packetStream = new MemoryStream(packet);
            var packetId = await ReadVarInt(packetStream, timeout.Token);
            if (packetId != 0)
                return new PingResult(false, 0, 0, 0, "", "", "服务器未返回状态信息");
            var json = await ReadString(packetStream, timeout.Token);
            watch.Stop();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var online = 0;
            var max = 0;
            if (root.TryGetProperty("players", out var players))
            {
                if (players.TryGetProperty("online", out var o) && o.TryGetInt32(out var onlineValue))
                    online = onlineValue;
                if (players.TryGetProperty("max", out var m) && m.TryGetInt32(out var maxValue))
                    max = maxValue;
            }

            var version = root.TryGetProperty("version", out var versionNode) && versionNode.TryGetProperty("name", out var versionName)
                ? versionName.GetString() ?? ""
                : "";
            var motd = Regex.Replace(ReadMotd(root), "§.", "");
            return new PingResult(true, online, max, (int)watch.ElapsedMilliseconds, motd, version, "");
        }
        catch (OperationCanceledException)
        {
            return new PingResult(false, 0, 0, 0, "", "", "连接超时（8 秒）");
        }
        catch (Exception ex) when (ex is SocketException or IOException or JsonException)
        {
            return new PingResult(false, 0, 0, 0, "", "", ex.Message);
        }
    }

    static string ReadMotd(JsonElement root)
    {
        if (!root.TryGetProperty("description", out var description))
            return "";
        if (description.ValueKind == JsonValueKind.String)
            return description.GetString() ?? "";
        if (description.ValueKind == JsonValueKind.Object)
        {
            if (description.TryGetProperty("text", out var text))
                return text.GetString() ?? "";
            if (description.TryGetProperty("extra", out var extra) && extra.ValueKind == JsonValueKind.Array)
                return string.Concat(extra.EnumerateArray().Select(ReadComponent));
        }
        return "";
    }

    static string ReadComponent(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
            return element.GetString() ?? "";
        if (element.ValueKind != JsonValueKind.Object)
            return "";
        var text = element.TryGetProperty("text", out var value) ? value.GetString() ?? "" : "";
        if (element.TryGetProperty("extra", out var extra) && extra.ValueKind == JsonValueKind.Array)
            text += string.Concat(extra.EnumerateArray().Select(ReadComponent));
        return text;
    }

    static (string Host, int Port) Split(string address)
    {
        address = address.Trim();
        if (address.Length == 0)
            return ("", 25565);
        if (address.StartsWith('['))
        {
            var end = address.IndexOf(']');
            if (end > 0)
            {
                var host = address[1..end];
                var rest = address[(end + 1)..];
                return rest.StartsWith(':') && int.TryParse(rest[1..], out var port) ? (host, port) : (host, 25565);
            }
        }

        var colon = address.LastIndexOf(':');
        if (colon > 0 && address.IndexOf(':') == colon && int.TryParse(address[colon..][1..], out var parsed))
            return (address[..colon], parsed);
        return (address, 25565);
    }

    static async Task WritePacket(NetworkStream stream, byte[] payload, CancellationToken token)
    {
        var header = new MemoryStream();
        WriteVarInt(header, payload.Length);
        await stream.WriteAsync(header.ToArray(), token);
        await stream.WriteAsync(payload, token);
    }

    static async Task<int> ReadVarInt(Stream stream, CancellationToken token)
    {
        var result = 0;
        for (var i = 0; i < 5; i++)
        {
            var buffer = new byte[1];
            await stream.ReadExactlyAsync(buffer, token);
            result |= (buffer[0] & 0x7F) << (7 * i);
            if ((buffer[0] & 0x80) == 0)
                return result;
        }
        return result;
    }

    static async Task<byte[]> ReadExactly(Stream stream, int length, CancellationToken token)
    {
        var buffer = new byte[length];
        await stream.ReadExactlyAsync(buffer, token);
        return buffer;
    }

    static async Task<string> ReadString(Stream stream, CancellationToken token)
    {
        var length = await ReadVarInt(stream, token);
        if (length < 0 || length > 2 * 1024 * 1024)
            return "";
        var bytes = await ReadExactly(stream, length, token);
        return Encoding.UTF8.GetString(bytes);
    }

    static void WriteVarInt(Stream stream, int value)
    {
        var unsigned = (uint)value;
        do
        {
            var temp = (byte)(unsigned & 0x7F);
            unsigned >>= 7;
            if (unsigned != 0)
                temp |= 0x80;
            stream.WriteByte(temp);
        } while (unsigned != 0);
    }

    static void WriteString(Stream stream, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteVarInt(stream, bytes.Length);
        stream.Write(bytes);
    }

    static void WriteUInt16(Stream stream, ushort value)
    {
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)(value & 0xFF));
    }
}
