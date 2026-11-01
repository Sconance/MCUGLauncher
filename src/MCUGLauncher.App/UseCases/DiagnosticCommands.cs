using BlockLaunch;
using Microsoft.Web.WebView2.Core;

namespace MCUGLauncher.UseCases;

// Handles diagnostic use cases; no WPF controls or browser access.
internal sealed class DiagnosticCommands(LauncherContext context)
{
    public void ReadLog()
    {
        try
        {
            var path = Path.Combine(context.Store.DataDirectory, "launcher.log");
            var text = File.Exists(path) ? string.Join('\n', File.ReadLines(path).TakeLast(400)) : "还没有日志。启动或启动游戏后会写入这里。";
            context.Send(new
            {
                type = "logText",
                text
            });
        }
        catch (Exception ex)
        {
            context.Send(new
            {
                type = "logText",
                text = "读取日志失败：" + ex.Message
            });
        }
    }

    public void SendSystemInfo()
    {
        var webview = "未知";
        try
        {
            webview = CoreWebView2Environment.GetAvailableBrowserVersionString();
        }
        catch { }
        context.Send(new
        {
            type = "systemInfo",
            app = ProductInfo.Version,
            dotnet = Environment.Version.ToString(),
            webview,
            os = Environment.OSVersion.VersionString,
            root = context.Config.Root,
            data = context.Store.DataDirectory
        });
    }
}
