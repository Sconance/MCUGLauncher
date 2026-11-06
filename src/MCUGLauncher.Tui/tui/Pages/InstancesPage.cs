using MCUGLauncher.Contracts;

namespace MCUGLauncher.Tui.Pages;

/// <summary>实例选择、启动、自动进服设置及卸载预览。</summary>
internal sealed class InstancesPage(ILauncherService service, Terminal terminal) : TerminalPage(service, terminal)
{
    public override string Title => "实例与启动";

    public override async Task ShowAsync()
    {
        await Service.RequestStateAsync();
        var instances = State.Instances.Where(item => !item.IsPlaceholder).ToArray();
        var index = IO.Select(Title, instances.Select(item => $"{item.DisplayName} | {item.Minecraft} | {item.Loader}").ToArray());
        if (index < 0)
            return;
        var instance = instances[index];
        var action = IO.Select(instance.DisplayName, ["设为当前实例", "启动游戏", "设置启动后自动进入的服务器", "预览并卸载实例"]);
        switch (action)
        {
            case 0:
                await Service.SelectInstanceAsync(instance.Name);
                IO.Write("已请求切换实例。");
                break;
            case 1:
                var result = await Service.LaunchAsync(instance.Name);
                IO.Write((result.Ok ? "成功：" : "失败：") + result.Message);
                break;
            case 2:
                var options = await Service.ReadLaunchOptionsAsync(instance.Name);
                var settings = options.Settings;
                IO.Write($"当前地址：{settings.Server}；继承全局设置：{options.Inherited}");
                var address = IO.Read("自动进服地址（留空清除）：");
                if (!IO.Confirm("保存为此实例独立设置？其他启动参数沿用当前值。"))
                    return;
                var saved = await Service.SaveLaunchOptionsAsync(options.Token, instance.Name, options.Name,
                    new LaunchOptionsSettings(settings.AutoMemory, settings.FullScreen, settings.RamMb,
                        settings.Width, settings.Height, settings.Java, settings.JvmArguments,
                        settings.WindowTitle, address, settings.AutoChinese), false);
                IO.Write(saved.Message);
                break;
            case 3:
                var preview = await Service.PreviewUninstallAsync(instance.Name);
                IO.Write(preview.Notice);
                foreach (var path in preview.Paths)
                    IO.Write(path);
                IO.Write("提交后仍需通过应用服务的卸载确认。");
                await Service.ApplyUninstallAsync(preview.Token);
                IO.Write("卸载请求已处理，请在任务页检查结果。");
                break;
            default:
                return;
        }
        IO.Pause();
    }
}
