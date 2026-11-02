using Avalonia.Controls;

namespace MCUGLauncher.Avalonia.Views;
internal static class InstallDialogs
{
    public static async Task Import(LauncherFacade session, Window owner)
    {
        var path = await FilePicker.Open(owner, "整合包", "*.zip", "*.mrpack");
        if (path == null)
            return;
        await ImportPath(session, owner, path);
    }

    public static async Task ImportPath(LauncherFacade session, Window owner, string path)
    {
        var result = await session.RequestAsync("packPreview", new
        {
            type = "previewPack",
            path
        });
        var name = Ui.Input(result.Name);
        var directory = Ui.Input(result.Root);
        var content = Ui.Stack(
            Ui.Field("实例名称", name),
            Ui.Field("游戏根目录", directory),
            Ui.Button("选择目录", async () => directory.Text = await FilePicker.Folder((Window)TopLevel.GetTopLevel(name)!) ?? directory.Text),
            Ui.Text(result.Game + " · " + result.Loader)
        );
        var confirm = Ui.Button("开始导入", async () =>
                {
                    await session.RequestAsync("packQueued", new
                    {
                        type = "confirmPack",
                        token = result.Token,
                        name = name.Text ?? "",
                        directory = directory.Text ?? ""
                    }, m => m.Token == result.Token);
                    (TopLevel.GetTopLevel(name) as Window)?.Close();
                }, true);
        await Ui.Show(owner, "导入整合包", content, 600, 430, Ui.DialogActions(content, confirm), "确认名称与目标目录后开始导入。");
    }

    public static async Task Loader(LauncherFacade session, Window owner, string version)
    {
        var loader = Ui.Select(new[] { "fabric", "quilt", "forge", "neoforge" });
        var releases = Ui.Select(Array.Empty<string>());
        var status = Ui.Text("选择加载器并读取可用版本。");
        var install = Ui.Button("安装所选加载器", async () =>
            {
                if (releases.SelectedItem is not string selected)
                    throw new ArgumentException("请先读取加载器版本。");
                await session.RequestAsync("loaderQueued", new
                {
                    type = "installLoader",
                    version,
                    loader = loader.SelectedItem as string ?? "",
                    loaderVersion = selected
                });
                (TopLevel.GetTopLevel(releases) as Window)?.Close();
            }, true);
        var revision = 0;
        async Task ReadVersions()
        {
            var generation = ++revision;
            releases.ItemsSource = Array.Empty<string>();
            install.IsEnabled = false;
            status.Text = "正在读取兼容版本…";
            var kind = loader.SelectedItem as string ?? "fabric";
            try
            {
                var request = Guid.NewGuid().ToString("N");
                var result = await session.RequestAsync("loaderList", new
                {
                    type = "listLoaders",
                    version,
                    loader = kind,
                    requestId = request
                }, m => m.Version == version && m.Loader == kind && m.RequestId == request);
                if (generation != revision)
                    return;
                releases.ItemsSource = result.Versions;
                releases.SelectedIndex = result.Versions.Length > 0 ? 0 : -1;
                install.IsEnabled = result.Versions.Length > 0;
                status.Text = result.Versions.Length > 0 ? $"{result.Versions.Length} 个兼容版本" : "此游戏版本没有兼容版本，请选择其他加载器";
            }
            catch (Exception error) { if (generation == revision) status.Text = "读取失败：" + error.Message; }
        }
        loader.SelectionChanged += (_, _) => _ = ReadVersions();
        var read = Ui.Button("读取加载器版本", ReadVersions);
        _ = ReadVersions();
        var content = Ui.Stack(
                Ui.Muted("安装会生成对应加载器版本并保留原版；只有兼容的加载器版本才能安装。"),
                Ui.Field("加载器", loader),
                read,
                Ui.Field("加载器版本", releases),
                status
        );
        await Ui.Show(owner, "安装模组加载器", content, 560, 450, Ui.DialogActions(content, install), "Minecraft " + version + " · 仅列出兼容版本。");
    }
}
