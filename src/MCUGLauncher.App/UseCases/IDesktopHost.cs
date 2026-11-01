

namespace MCUGLauncher.UseCases;

// Native window operations stay in the shell, outside application use cases.
internal interface IDesktopHost
{
    bool ConfirmStopGame();
    bool ConfirmUninstall(string name);
    void PickSaveFile(string target);
    void Window(string action);
    void Drag();
    void PickFolder(string target);
    void PickFile(string target);
    void ResetWindow();
}
