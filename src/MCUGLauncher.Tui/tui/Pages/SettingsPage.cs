using MCUGLauncher.Contracts;

namespace MCUGLauncher.Tui.Pages;

/// <summary>展示设置快照并提交补丁；校验、扫描及持久化由应用服务负责。</summary>
internal sealed class SettingsPage(ILauncherService service, Terminal terminal) : TerminalPage(service, terminal)
{
    public override string Title => "设置与 Java";

    public override async Task ShowAsync()
    {
        await Service.RequestStateAsync();
        IO.Write($"游戏目录：{State.Root}");
        IO.Write($"Java：{State.Java} | 内存：{State.Ram} MB | 自动内存：{State.AutoMemory}");
        IO.Write($"窗口：{State.Width} × {State.Height} | 自动中文：{State.AutoChinese}");
        var action = IO.Select(Title, ["切换游戏目录", "扫描并选择 Java", "设置内存", "设置窗口尺寸", "设置自动中文"]);
        AppSettingsPatch? patch = null;
        switch (action)
        {
            case 0:
                var directory = IO.Required("已有游戏目录完整路径：");
                if (IO.Confirm("切换游戏目录？"))
                    patch = new(Root: directory);
                break;
            case 1:
                var java = await Service.ScanJavaAsync();
                var selected = IO.Select("选择 Java", new[] { "自动管理" }.Concat(java.Select(item =>
                    $"Java {item.Major} | {item.Arch} | {item.Path} | {item.Error}")).ToArray());
                if (selected >= 0)
                    patch = new(Java: selected == 0 ? "" : java[selected - 1].Path);
                break;
            case 2:
                var automatic = IO.Confirm("自动分配内存？");
                patch = automatic ? new(AutoMemory: true) : new(AutoMemory: false,
                    Ram: IO.Number("内存 MB：", LaunchLimits.RamMin, LaunchLimits.RamMax));
                break;
            case 3:
                patch = new(Width: IO.Number("窗口宽度：", LaunchLimits.WindowWidthMin, LaunchLimits.WindowWidthMax),
                    Height: IO.Number("窗口高度：", LaunchLimits.WindowHeightMin, LaunchLimits.WindowHeightMax));
                break;
            case 4:
                patch = new(AutoChinese: IO.Confirm("启用自动简体中文？"));
                break;
            default:
                return;
        }
        if (patch is not null)
            IO.Write((await Service.SaveSettingsAsync(patch)).Message);
        IO.Pause();
    }
}
