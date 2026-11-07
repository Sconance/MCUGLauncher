using MCUGLauncher.Contracts;

namespace MCUGLauncher.Tui.Pages;

/// <summary>原版安装、加载器安装与整合包预览确认；执行统一交给应用任务队列。</summary>
internal sealed class DownloadsPage(ILauncherService service, Terminal terminal) : TerminalPage(service, terminal)
{
    public override string Title => "下载与安装";

    public override async Task ShowAsync()
    {
        var action = IO.Select(Title, ["浏览原版目录并安装", "安装加载器", "导入整合包"]);
        switch (action)
        {
            case 0:
                await InstallVanilla();
                break;
            case 1:
                await InstallLoader();
                break;
            case 2:
                await ImportPack();
                break;
            default:
                return;
        }
        IO.Pause();
    }

    async Task InstallVanilla()
    {
        var result = await Service.ListVanillaVersionsAsync();
        if (!result.Ok)
            throw new InvalidOperationException(result.Error);
        var query = IO.Read("搜索版本（留空显示全部）：");
        var versions = result.Items.Where(item => item.Id.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
        const int pageSize = 20;
        for (var offset = 0; offset < versions.Length;)
        {
            var items = versions.Skip(offset).Take(pageSize).ToArray();
            var labels = items.Select(item => $"{item.Id} | {item.Type} | {item.Released}").ToList();
            if (offset + items.Length < versions.Length)
                labels.Add("下一页");
            var selected = IO.Select($"原版目录（共 {versions.Length} 项，第 {offset / pageSize + 1} 页）", labels);
            if (selected < 0)
                return;
            if (selected == items.Length)
            {
                offset += pageSize;
                continue;
            }
            if (IO.Confirm($"安装 {items[selected].Id} 到 {State.Root}？"))
            {
                await Service.InstallVanillaAsync(items[selected].Id);
                IO.Write("安装请求已提交，请到任务页查看状态。");
            }
            return;
        }
        IO.Write("没有匹配的版本。");
    }

    async Task InstallLoader()
    {
        var version = IO.Required("Minecraft 版本，例如 1.21.1：");
        var loaders = new[] { "fabric", "quilt", "forge", "neoforge" };
        var selected = IO.Select("加载器", loaders);
        if (selected < 0)
            return;
        var loader = loaders[selected];
        var result = await Service.ListLoadersAsync(version, loader);
        if (!result.Ok || result.Result is null)
            throw new InvalidOperationException(result.Error);
        var index = IO.Select("加载器版本", result.Result.Versions);
        if (index >= 0 && IO.Confirm($"安装 {version} / {loader} {result.Result.Versions[index]}？"))
        {
            await Service.InstallLoaderAsync(version, loader, result.Result.Versions[index]);
            IO.Write("安装请求已提交，请到任务页查看状态。");
        }
    }

    async Task ImportPack()
    {
        var result = await Service.PreviewPackAsync(IO.Required("整合包完整路径："));
        if (!result.Ok || result.Preview is null)
            throw new InvalidOperationException(result.Error);
        var preview = result.Preview;
        IO.Write($"预览：{preview.Name} | {preview.Game} | {preview.Loader}");
        var name = IO.Read($"实例名称（留空使用 {preview.Name}）：");
        var directory = IO.Read($"目标根目录（留空使用 {preview.Root}）：");
        if (IO.Confirm("确认导入此整合包？"))
        {
            await Service.ConfirmPackAsync(preview.Token, name.Length == 0 ? preview.Name : name,
                directory.Length == 0 ? preview.Root : directory);
            IO.Write("导入请求已提交，请到任务页查看状态。");
        }
    }
}
