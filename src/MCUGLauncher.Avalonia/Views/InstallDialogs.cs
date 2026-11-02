using Avalonia.Controls;

namespace MCUGLauncher.Avalonia.Views;
internal static class InstallDialogs
{
    public static async Task Import(LauncherFacade session, Window owner)
    {
        var path = await FilePicker.Open(owner, "整合包", "*.zip", "*.mrpack");
        if (path == null)
            return;
        var result = await session.RequestAsync("packPreview", new
        {
            type = "previewPack",
            path
        });
        var name = Ui.Input(result.Name);
        var directory = Ui.Input(result.Root);
        var content = Ui.Stack(
            Ui.Heading("确认整合包导入"),
            Ui.Field("实例名称", name),
            Ui.Field("游戏根目录", directory),
            Ui.Button("选择目录", async () => directory.Text = await FilePicker.Folder((Window)TopLevel.GetTopLevel(name)!) ?? directory.Text),
            Ui.Text(result.Game + " · " + result.Loader)
        );
        content.Children.Add(Ui.Button("确认导入", async () =>
                {
                    await session.RequestAsync("packQueued", new
                    {
                        type = "confirmPack",
                        token = result.Token,
                        name = name.Text ?? "",
                        directory = directory.Text ?? ""
                    }, m => m.Token == result.Token);
                    (TopLevel.GetTopLevel(name) as Window)?.Close();
                }, true));
        await Ui.Show(owner, "整合包导入", content);
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
                await session.SendAsync(new
                {
                    type = "installLoader",
                    version,
                    loader = loader.SelectedItem as string ?? "",
                    loaderVersion = selected
                });
                status.Text = "已加入下载任务。";
            }, true);
        loader.SelectionChanged += (_, _) =>
        {
            releases.ItemsSource = Array.Empty<string>();
            install.IsEnabled = false;
        };
        install.IsEnabled = false;
        var read = Ui.Button("读取加载器版本", async () =>
            {
                var kind = loader.SelectedItem as string ?? "fabric";
                var result = await session.RequestAsync("loaderList", new
                {
                    type = "listLoaders",
                    version,
                    loader = kind
                }, m => m.Version == version && m.Loader == kind);
                releases.ItemsSource = result.Versions;
                releases.SelectedIndex = result.Versions.Length > 0 ? 0 : -1;
                install.IsEnabled = result.Versions.Length > 0;
                status.Text = $"{result.Versions.Length} 个版本";
            });
        await Ui.Show(owner, "版本安装配置 · " + version, Ui.Stack(
                Ui.Heading(version),
                Ui.Field("加载器", loader),
                read,
                Ui.Field("加载器版本", releases),
                status,
                install
        ));
    }
}
