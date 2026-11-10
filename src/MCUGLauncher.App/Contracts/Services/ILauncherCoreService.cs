namespace MCUGLauncher.Contracts;

/// <summary>启动、状态刷新、窗口位置与全局错误提示。</summary>
public interface ILauncherCoreService
{
    event Action<LauncherState>? StateChanged;
    event Action<ToastInfo>? ToastReceived;
    event Action<string>? ErrorReceived;

    WindowPlacement WindowPlacement
    {
        get;
    }
    LauncherState? State
    {
        get;
    }

    Task StartAsync();
    Task RequestStateAsync();
    void RememberWindow(double width, double height, bool maximized);
}
