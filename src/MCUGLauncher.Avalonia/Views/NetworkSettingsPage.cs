using Avalonia.Controls;

namespace MCUGLauncher.Avalonia.Views;

internal sealed class NetworkSettingsPage : NativePage
{
    public NetworkSettingsPage(LauncherFacade session) : base(session)
    {
        Title("下载与网络", "管理游戏文件下载、速度限制和代理；社区内容地址在数据源中设置。");
        var settings = State.Preferences.Network;
        var source = Ui.Select(new[] { "仅官方", "官方优先，失败切换 BMCLAPI", "BMCLAPI 优先" });
        source.SelectedItem = settings.DownloadSource;
        var limit = Ui.Input(settings.LimitKibPerSecond.ToString(), "0 表示不限速");
        var proxy = Ui.Select(new[] { "系统代理", "直连", "自定义代理" });
        proxy.SelectedItem = settings.ProxyMode;
        var address = Ui.Input(settings.ProxyAddress, "http://127.0.0.1:7890");
        address.IsEnabled = proxy.SelectedIndex == 2;
        proxy.SelectionChanged += (_, _) => address.IsEnabled = proxy.SelectedIndex == 2;
        var directory = Ui.Input(State.Preferences.ResourceDownloadDirectory);
        Body.Children.Add(Ui.SettingsCard("游戏文件下载",
            Ui.Setting("下载源", "镜像仅作用于受支持的游戏文件，不转发账号认证", source),
            Ui.Setting("速度上限（KiB/s）", "0 表示不限速，1 MiB/s = 1024 KiB/s", limit)));
        Body.Children.Add(Ui.SettingsCard("网络代理",
            Ui.Setting("代理方式", "代理修改需重启启动器后生效", proxy),
            Ui.Field("代理地址（不含账号和密码）", address)));
        Body.Children.Add(Ui.SettingsCard("资源保存位置", Ui.Text("自定义下载模式默认使用此目录；安装到实例时仍使用实例目录。"),
            directory, Ui.Button("选择文件夹", async () => directory.Text = await FilePicker.Folder(Owner) ?? directory.Text)));
        Body.Children.Add(Ui.Row(Ui.Button("保存网络设置", async () =>
        {
            if (!int.TryParse(limit.Text, out var speed))
                throw new ArgumentException("速度上限必须是整数。");
            await Send(new
            {
                type = "saveSettings",
                network = new
                {
                    downloadSource = source.SelectedItem as string ?? "",
                    proxyMode = proxy.SelectedItem as string ?? "",
                    proxyAddress = address.Text ?? "",
                    limitKibPerSecond = speed
                },
                resourceDownloadDirectory = directory.Text ?? ""
            });
        }, true), Ui.Action("社区数据源", () => Session.Navigate?.Invoke("sources"))));
    }
}
