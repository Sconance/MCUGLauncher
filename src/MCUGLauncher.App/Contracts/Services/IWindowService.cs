namespace MCUGLauncher.Contracts;

/// <summary>窗口位置、文件/文件夹选择与复位。</summary>
public interface IWindowService
{
    event Action<PickedPathInfo>? PathPicked;

    Task PickFolderAsync(string target);
    Task PickFileAsync(string target);
    Task PickSaveFileAsync(string target);
    Task ResetWindowAsync();
}
