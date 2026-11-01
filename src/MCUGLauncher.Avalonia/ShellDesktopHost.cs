using MCUGLauncher.UseCases;

namespace MCUGLauncher.Avalonia;

/// <summary>Avalonia 宿主适配：确认框与文件选择走 Win32，窗口操作回调到 ShellWindow。</summary>
internal sealed class ShellDesktopHost(
    Func<IntPtr> handle,
    Action<object> send,
    Action<string> window,
    Action drag,
    Action reset) : IDesktopHost
{
    public bool ConfirmStopGame() =>
        NativeDialogs.Confirm(handle(), "确认关闭这个游戏？未保存的内容可能丢失。", "关闭游戏");

    public bool ConfirmUninstall(string name) =>
        NativeDialogs.Confirm(handle(), "确认执行「" + name + "」的卸载计划？列出的文件将永久删除，请先确认备份。", "确认卸载");

    public void PickSaveFile(string target)
    {
        var path = NativeDialogs.SaveFile(handle(), "整合包 ZIP|*.zip", "instance-export.zip", null);
        if (path is not null)
            send(new
            {
                type = "pickedPath",
                target,
                path
            });
    }

    public void PickFolder(string target)
    {
        var path = NativeDialogs.PickFolder(handle(), "选择文件夹");
        if (path is not null)
            send(new
            {
                type = "pickedPath",
                target,
                path
            });
    }

    public void PickFile(string target)
    {
        var filter = target switch
        {
            "java" => "Java|java.exe;javaw.exe|所有文件|*.*",
            "offlineSkin" => "PNG 皮肤|*.png",
            "pack" => "整合包|*.mrpack;*.zip",
            _ => "所有文件|*.*"
        };
        var path = NativeDialogs.OpenFile(handle(), filter, null);
        if (path is not null)
            send(new
            {
                type = "pickedPath",
                target,
                path
            });
    }

    public void Window(string action) => window(action);
    public void Drag() => drag();
    public void ResetWindow() => reset();
}
