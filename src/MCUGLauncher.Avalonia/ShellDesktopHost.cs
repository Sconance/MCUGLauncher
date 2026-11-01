using MCUGLauncher.UseCases;

namespace MCUGLauncher.Avalonia;

/// <summary>阶段 1 的最小原生宿主：暂不弹出系统对话框，后续按页面补齐。</summary>
internal sealed class ShellDesktopHost : IDesktopHost
{
    public bool ConfirmStopGame() => false;
    public bool ConfirmUninstall(string name) => false;
    public void PickSaveFile(string target)
    {
    }
    public void Window(string action)
    {
    }
    public void Drag()
    {
    }
    public void PickFolder(string target)
    {
    }
    public void PickFile(string target)
    {
    }
    public void ResetWindow()
    {
    }
}
