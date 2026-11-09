using MCUGLauncher.Contracts;

namespace MCUGLauncher.Tui.Pages;

/// <summary>按目录浏览实例，向应用接口传递完整目标，不以同名实例替换目标。</summary>
internal sealed class InstancesPage(ILauncherService service, Terminal terminal) : TerminalPage(service, terminal)
{
    public override string Title => "实例与启动";

    public override async Task ShowAsync()
    {
        await Service.RequestStateAsync();
        var directories = State.GameDirectories;
        var directory = IO.Select("选择实例所属游戏目录", directories.Select(item => GameDirectoriesPage.Label(item, State.Root)).ToArray());
        if (directory < 0)
            return;
        await ShowDirectoryAsync(directories[directory]);
    }

    public async Task ShowDirectoryAsync(GameDirectoryInfo directory)
    {
        IO.Write("所属游戏目录：" + directory.Root);
        if (!directory.Available)
        {
            IO.Write("目录不可用：" + directory.Error);
            IO.Pause();
            return;
        }
        var instances = directory.Instances.Where(item => !item.IsPlaceholder).ToArray();
        if (instances.Length == 0)
        {
            IO.Write("此目录暂时没有可选择的实例。请到下载与安装页安装，或添加其他游戏目录。");
            IO.Pause();
            return;
        }
        var index = IO.Select(Title, instances.Select(item => $"{item.DisplayName} | {item.Minecraft} | {item.Loader}").ToArray());
        if (index < 0)
            return;
        var instance = instances[index];
        var target = new InstanceTarget(directory.Root, instance.Name);
        IO.Write($"目标：{target.Root} | {target.Name}");
        var action = IO.Select(instance.DisplayName, ["设为当前实例", "启动游戏", "设为当前实例并编辑自动进服设置", "预览并卸载实例", "克隆实例", "导出整合包", "管理模组"]);
        switch (action)
        {
            case 0:
                IO.Write((await Service.SelectManagedInstanceAsync(target)).Message);
                break;
            case 1:
                var result = await Service.LaunchManagedInstanceAsync(target);
                IO.Write((result.Ok ? "成功：" : "失败：") + result.Message);
                break;
            case 2:
                IO.Write((await Service.SelectManagedInstanceAsync(target)).Message);
                IO.Write("已切换到：" + target.Root);
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
                var preview = await Service.PreviewManagedUninstallAsync(target);
                IO.Write(preview.Notice);
                foreach (var path in preview.Paths)
                    IO.Write(path);
                IO.Write("提交后仍需通过应用服务的卸载确认。");
                await Service.ApplyUninstallAsync(preview.Token);
                IO.Write("卸载请求已处理，请在任务页检查结果。");
                break;
            case 4:
                var name = IO.Required("新实例显示名称：");
                var saves = IO.Confirm("复制存档？");
                var servers = IO.Confirm("复制游戏内服务器列表？");
                IO.Write((await Service.CloneManagedInstanceAsync(target, name, saves, servers)).Message);
                IO.Write("请到任务页检查进度；任务目标为：" + target.Root);
                break;
            case 5:
                var exportPath = IO.Read("导出 ZIP 完整路径（留空取消）：");
                if (exportPath.Length == 0)
                    return;
                var exportServers = IO.Confirm("包含游戏内服务器列表？");
                var exportSaves = IO.Confirm("包含存档？");
                IO.Write((await Service.ExportManagedInstanceAsync(target, exportPath, exportServers, exportSaves)).Message);
                IO.Write("请到任务页检查导出结果。");
                break;
            case 6:
                await ManageModsAsync(target);
                break;
            default:
                return;
        }
        IO.Pause();
    }

    async Task ManageModsAsync(InstanceTarget target)
    {
        var mods = await Service.ListManagedModsAsync(target);
        if (mods.Count == 0)
        {
            IO.Write("此实例没有模组。");
            return;
        }
        var index = IO.Select("模组：" + target.Root + " | " + target.Name,
            mods.Select(item => $"{(item.Enabled ? "启用" : "禁用")} | {item.File} | {item.Version}").ToArray());
        if (index < 0)
            return;
        var mod = mods[index];
        var action = IO.Select(mod.File, [mod.Enabled ? "禁用模组" : "启用模组", "删除模组文件"]);
        if (action == 0)
            IO.Write((await Service.ToggleManagedModAsync(target, mod.File, !mod.Enabled)).Message);
        else if (action == 1)
        {
            IO.Write("将删除文件：" + mod.Path);
            if (IO.Confirm("确认从此实例删除模组？"))
                IO.Write((await Service.DeleteManagedModAsync(target, mod.File)).Message);
        }
    }
}
