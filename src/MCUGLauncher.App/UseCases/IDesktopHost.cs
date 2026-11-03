

namespace MCUGLauncher.UseCases;

// Native window operations stay in the shell, outside application use cases.
public interface IDesktopHost
{
    Task<bool> ConfirmStopGameAsync();
    Task<bool> ConfirmUninstallAsync(string name);
    Task<string?> PickSaveFileAsync(string target);
    void Window(string action);
    void Drag();
    Task<string?> PickFolderAsync(string target);
    Task<string?> PickFileAsync(string target);
    void ResetWindow();
}
