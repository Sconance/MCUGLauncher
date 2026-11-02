using Avalonia.Controls;
using MCUGLauncher.Avalonia.Presentation;
namespace MCUGLauncher.Avalonia.Views;

// Restores the separate settings routes used by the codex UI, rather than merging them.
internal sealed class UtilitySettingsPage : NativePage
{
    readonly TextBox output = Ui.Input();
    public UtilitySettingsPage(LauncherFacade session, string panel) : base(session)
    {
        output.AcceptsReturn = true;
        output.IsReadOnly = true;
        output.MinHeight = 300;
        output.TextWrapping = global::Avalonia.Media.TextWrapping.Wrap;
        switch (panel)
        {
            case "install":
                Install();
                break;
            case "network":
                Network();
                break;
            case "appearance":
                Title("界面与背景", "蓝白主题与窗口显示。");
                Body.Children.Add(Ui.Card(Ui.Stack(Ui.Text("蓝白 · 默认"), Ui.Text("#006DB8 强调色 / #F3F7F9 页面底色"), Ui.Button("恢复默认窗口尺寸", () => Send(new { type = "resetWindow" })))));
                break;
            case "recovery":
                Title("任务与恢复", "窗口、界面与配置目录。");
                Body.Children.Add(Ui.Row(Ui.Button("恢复窗口尺寸", () => Send(new { type = "resetWindow" })), Ui.Action("重新加载界面", () => Session.Navigate?.Invoke("recovery")), Ui.Button("打开配置目录", OpenData)));
                break;
            case "logs":
                Title("运行日志", "最近 400 行启动与游戏日志。");
                Body.Children.Add(Ui.Row(Ui.Button("刷新日志", async () => output.Text = (await Session.RequestAsync("logText", new { type = "readLog" })).Text), Ui.Button("打开日志目录", OpenData)));
                Body.Children.Add(output);
                break;
            case "diagnose":
                Title("诊断与反馈", "查看运行环境，反馈前请检查是否含个人信息。");
                Body.Children.Add(Ui.Row(Ui.Button("读取系统信息", async () => { var info = await Session.RequestAsync("systemInfo", new { type = "systemInfo" }); output.Text = $"版本：{info.App}\n.NET：{info.Dotnet}\n系统：{info.Os}\n游戏目录：{info.Root}\n数据目录：{info.Data}"; }), Ui.Button("复制", async () => { if (Owner.Clipboard != null) await Owner.Clipboard.SetTextAsync(output.Text ?? ""); })));
                Body.Children.Add(output);
                break;
            case "about":
                Title("关于与许可", BlockLaunch.ProductInfo.Name + " · " + BlockLaunch.ProductInfo.Version + " · AI 迁移框架尝试");
                Body.Children.Add(Ui.Logo(70));
                Body.Children.Add(Ui.Button("查看第三方许可", async () => { using var stream = global::Avalonia.Platform.AssetLoader.Open(new Uri("avares://MCUGLauncher/Assets/Notices.txt")); using var reader = new StreamReader(stream); output.Text = await reader.ReadToEndAsync(); }));
                Body.Children.Add(output);
                break;
        }
    }
    async Task OpenData()
    {
        var info = await Session.RequestAsync("systemInfo", new
        {
            type = "systemInfo"
        });
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(info.Data) { UseShellExecute = true });
    }
    void Install()
    {
        Title("安装与导入", "选择游戏版本安装加载器，或导入本地整合包。");
        var game = Ui.Input("", "例如 1.21.1");
        Body.Children.Add(Ui.Card(Ui.Stack(Ui.Field("游戏版本", game), Ui.Button("配置 Fabric / Quilt / Forge / NeoForge", () => InstallDialogs.Loader(Session, Owner, game.Text ?? "")))));
        Body.Children.Add(Ui.Card(Ui.Stack(Ui.Heading("本地整合包"), Ui.Text("支持的 ZIP / mrpack 会先显示名称与目录确认。"), Ui.Button("选择整合包", () => InstallDialogs.Import(Session, Owner)), Ui.Action("查看任务", () => Session.Navigate?.Invoke("tasks")))));
    }
    void Network()
    {
        Title("下载与网络", "数据源与连接诊断。");
        var sources = State.DataSources;
        Body.Children.Add(Ui.Card(Ui.Stack(Ui.Text("公告：" + sources.Announcements), Ui.Text("服务器：" + sources.Servers), Ui.Text("推荐：" + sources.Recommendations), Ui.Text("语录：" + sources.Quotes), Ui.Action("打开数据源设置", () => Session.Navigate?.Invoke("sources")))));
        Body.Children.Add(Ui.Button("测试四类地址", async () =>
        {
            var lines = new List<string>();
            foreach (var (name, url) in new[] { ("announcements", sources.Announcements), ("servers", sources.Servers), ("recommendations", sources.Recommendations), ("quotes", sources.Quotes) })
            {
                var requestId = Guid.NewGuid().ToString("N");
                var result = await Session.RequestAsync("testResult", new
                {
                    type = "testSource",
                    name,
                    url,
                    requestId
                }, m => m.RequestId == requestId);
                lines.Add(name + "：" + result.Message);
            }
            output.Text = string.Join('\n', lines);
        }));
        Body.Children.Add(output);
    }
}
internal sealed class CommunityOptionsPage : NativePage
{
    public CommunityOptionsPage(LauncherFacade session) : base(session)
    {
        Title("账号与社区设置", "本地账号与社区密钥；保存密钥不等于服务器身份认证。");
        Body.Children.Add(Ui.Card(Ui.Stack(Ui.Heading("当前账号"), Ui.Text(State.Player + " · " + State.AccountLabel), Ui.Action("返回主页切换账号", () => Session.Navigate?.Invoke("home")), Ui.Text("切换到离线不会删除在线授权。"), Forget("移除保存的正版账号", "microsoft"), Forget("移除保存的 MUA 账号", "mua"))));
        Body.Children.Add(Ui.Card(Ui.Stack(Ui.Heading("社区请求"), Ui.Text(State.Anonymous ? "匿名模式：已开启" : "匿名模式：已关闭"), Ui.Button("切换匿名模式", () => Send(new { type = "saveSettings", anonymous = !State.Anonymous })))));
        var key = Ui.Input("", "粘贴社区密钥");
        key.PasswordChar = '●';
        var status = Ui.Text(State.CommunityKey ? "已保存在本机" : "尚未保存");
        Body.Children.Add(Ui.Card(Ui.Stack(Ui.Heading("社区密钥"), status, key, Ui.Button("保存密钥", async () => { var result = await Session.RequestAsync("keyResult", new { type = "verifyKey", role = "community", key = key.Text ?? "" }); key.Text = ""; status.Text = result.Message; }))));
    }
    Button Forget(string label, string account) => Ui.Button(label, async () => { if (await Ui.Confirm(Owner, "移除账号", "移除本机保存的账号？")) await Send(new { type = "forgetAccount", account }); });
}
internal sealed class VersionOverviewPage : NativePage
{
    public VersionOverviewPage(LauncherFacade session) : base(session)
    {
        var item = State.Instances.FirstOrDefault(i => i.Name == State.SelectedInstance);
        Title("版本设置 · " + (item?.Title ?? "未选择实例"));
        Body.Children.Add(Ui.Row(Ui.Action("返回", () => Session.Navigate?.Invoke("home")), Ui.Action("版本内容管理", () => Session.Navigate?.Invoke("manage"))));
        if (item == null)
        {
            Body.Children.Add(Ui.Text("请先选择实例。"));
            return;
        }
        Body.Children.Add(Ui.Card(Ui.Stack(Ui.Text(item.Title, 20), Ui.Text(item.Minecraft + " · " + item.Loader), Ui.Button("启动设置", () => LaunchOptionsPage.Open(Session, Owner, item.Name)), Ui.Button("打开版本文件夹", () => Send(new { type = "openFolder", kind = "version" })), Ui.Button("启动此版本", () => Send(new { type = "launch", version = item.Name }), true))));
    }
}
