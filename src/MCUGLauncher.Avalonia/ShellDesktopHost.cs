using Avalonia.Controls;
using MCUGLauncher.UseCases;
using MCUGLauncher.Avalonia.Views;
namespace MCUGLauncher.Avalonia;

// Avalonia confirmation and storage dialogs never block the UI thread.
internal sealed class ShellDesktopHost(Window owner, Action<object> send) : IDesktopHost
{
    public Task<bool> ConfirmStopGameAsync() => Ui.Confirm(owner, "关闭游戏", "确认关闭这个游戏？未保存的内容可能丢失。");
    public Task<bool> ConfirmUninstallAsync(string name) => Ui.Confirm(owner, "确认卸载", "确认执行「" + name + "」的卸载计划？列出的文件将永久删除，请先确认备份。");
    public void PickFolder(string target) => Pick(target, () => FilePicker.Folder(owner));
    public void PickSaveFile(string target) => Pick(target, () => FilePicker.Save(owner));
    public void PickFile(string target) => Pick(target, () => FilePicker.Open(owner, "选择文件", target switch { "java" => "*.exe", "offlineSkin" => "*.png", "pack" => "*.mrpack", _ => "*.*" }));
    async void Pick(string target, Func<Task<string?>> choose)
    {
        try
        {
            var path = await choose();
            if (path != null)
                send(new
                {
                    type = "pickedPath",
                    target,
                    path
                });
        }
        catch (Exception ex) { send(new { type = "error", message = ex.Message }); }
    }
    public void Window(string action)
    {
        if (action == "close")
            owner.Close();
        else if (action == "minimize")
            owner.WindowState = WindowState.Minimized;
        else if (action == "maximize")
            owner.WindowState = owner.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }
    public void Drag()
    {
    } // Native system title bar owns moving and resizing.
    public void ResetWindow()
    {
        owner.WindowState = WindowState.Normal;
        owner.Width = 1200;
        owner.Height = 780;
    }
}
