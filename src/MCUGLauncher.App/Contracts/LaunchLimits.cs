namespace MCUGLauncher.Contracts;

/// <summary>窗口位置记忆；UI 只能读取，写入统一走 <see cref="ILauncherService.RememberWindow"/>。</summary>
public sealed record WindowPlacement(double Width, double Height, bool Maximized)
{
    public static WindowPlacement Default { get; } = new(1200, 780, false);
}

/// <summary>启动设置与实例设置共用的边界，避免 UI 与后端各写一套范围。</summary>
public static class LaunchLimits
{
    public const int RamMin = 512;
    public const int RamMax = 65536;
    public const int WindowWidthMin = 640;
    public const int WindowWidthMax = 7680;
    public const int WindowHeightMin = 480;
    public const int WindowHeightMax = 4320;
    public const int DisplayNameMax = 120;
    public const int ShellWidthMin = 960;
    public const int ShellWidthMax = 7680;
    public const int ShellHeightMin = 640;
    public const int ShellHeightMax = 4320;
}
