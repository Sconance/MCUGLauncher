using BlockLaunch;
using MCUGLauncher.Contracts;

namespace MCUGLauncher.UseCases;

// Handles diagnostic use cases; no UI controls or browser access.
internal sealed class DiagnosticCommands(LauncherContext context)
{
    public string ReadLog()
    {
        try
        {
            var path = Path.Combine(context.Store.DataDirectory, "launcher.log");
            return File.Exists(path)
                ? string.Join('\n', File.ReadLines(path).TakeLast(400))
                : "还没有日志。启动或启动游戏后会写入这里。";
        }
        catch (Exception ex)
        {
            return "读取日志失败：" + ex.Message;
        }
    }

    public SystemInfoInfo SendSystemInfo() =>
        new(
            ProductInfo.Version,
            Environment.Version.ToString(),
            "仅微软登录窗口使用",
            Environment.OSVersion.VersionString,
            context.Config.Root,
            context.Store.DataDirectory);
}
