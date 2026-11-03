using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;
using MCUGLauncher;

namespace MCUGLauncher.Services;

/// <summary>供页面展示的服务器状态，不携带协议或连接对象。</summary>
public sealed record PingResult(bool Online, int OnlinePlayers, int MaxPlayers, int Ping, string Motd, string Version, string Error);

/// <summary>将 Core 的服务器查询结果适配为现有界面消息。</summary>
/// <remarks>
/// A 类，只依赖 Core/BCL。ServerFavorites.Ping 处理地址、协议、超时与数据包校验；
/// 此处保留原有 8 秒超时、去除颜色控制符和失败结果语义，不重复实现 SLP。
/// 不依赖配置、LauncherContext、Commands、视图或窗口。
/// </remarks>
public static class ServerPingService
{
    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(8);

    public static async Task<PingResult> PingAsync(string address, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(address))
            return Failed("地址为空");
        try
        {
            var status = await ServerFavorites.Ping(address, token, Timeout);
            return new(true, status.Online, status.Maximum, (int)Math.Clamp(status.LatencyMs, 0, int.MaxValue),
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
