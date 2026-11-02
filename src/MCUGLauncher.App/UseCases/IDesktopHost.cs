namespace MCUGLauncher.UseCases;

/// <summary>原生窗口与文件对话框端口；实现留在 Avalonia 宿主，业务层不直接依赖 Win32。</summary>
public interface IDesktopHost
{
    bool ConfirmStopGame();
    bool ConfirmUninstall(string name);
    string? PickSaveFile(string target);
    void Window(string action);
    void Drag();
    string? PickFolder(string target);
    string? PickFile(string target);
    void ResetWindow();
}
