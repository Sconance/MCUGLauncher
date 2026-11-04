using System.IO;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;
using BlockLaunch;

namespace MCUGLauncher.Services;

/// <summary>
/// 表示 服务器状态结果 数据。
/// </summary>
public sealed record PingResult(bool Online, int OnlinePlayers, int MaxPlayers, int Ping, string Motd, string Version, string Error);

/// <summary>
/// 将 Core 的服务器查询结果适配为现有界面消息。
/// </summary>
public static class ServerPingService
{
    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(8);

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
        if (string.IsNullOrWhiteSpace(address))
            return Failed("地址为空");
        try
        {
            var status = await ServerFavorites.Ping(address, token, Timeout);
            return new PingResult(true, status.Online, status.Maximum,
                (int)Math.Clamp(status.LatencyMs, 0, int.MaxValue),
                Regex.Replace(status.Description, "§.", ""), status.Version, "");
        }
        catch (OperationCanceledException)
        {
            return Failed(token.IsCancellationRequested ? "查询已取消" : "连接超时（8 秒）");
        }
        catch (Exception error) when (error is SocketException or IOException or JsonException or ArgumentException or InvalidOperationException or FormatException or OverflowException)
        {
            return Failed(error.Message);
        }
    }

    static PingResult Failed(string message) => new(false, 0, 0, 0, "", "", message);
}
