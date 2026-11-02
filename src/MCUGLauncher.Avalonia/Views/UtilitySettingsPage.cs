using Avalonia.Controls;
using MCUGLauncher.Avalonia.Presentation;
namespace MCUGLauncher.Avalonia.Views;

// Small utility routes share read-only output; each keeps its existing command contract.
internal sealed class UtilitySettingsPage : NativePage
{
    readonly TextBox output = Ui.Input();
    public UtilitySettingsPage(ILauncherService session, UiContext ui, string panel) : base(session, ui)
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
            case "recovery":
                Title("维护与恢复", "打开配置与日志目录，检查问题时保留原文件。");
                Body.Children.Add(Ui.SettingsCard("界面与窗口", Ui.Text("窗口大小在界面设置中统一恢复。"), Ui.Action("前往界面设置", () => UI.Navigate?.Invoke("appearance"))));
                Body.Children.Add(Ui.SettingsCard("问题排查", Ui.Row(Ui.Button("打开配置目录", OpenData), Ui.Action("查看运行日志", () => UI.Navigate?.Invoke("logs")), Ui.Action("诊断与反馈", () => UI.Navigate?.Invoke("diagnose")))));
                break;
            case "logs":
                Title("运行日志", "最近 400 行启动与游戏日志。");
                Body.Children.Add(Ui.Row(Ui.Button("刷新日志", async () => output.Text = (await Session.ReadLogAsync()).Text), Ui.Button("打开日志目录", OpenData)));
                Body.Children.Add(output);
                break;
            case "diagnose":
                Title("诊断与反馈", "查看运行环境，反馈前请检查是否含个人信息。");
                Body.Children.Add(Ui.Row(Ui.Button("读取系统信息", async () => { var info = await Session.SystemInfoAsync(); output.Text = $"版本：{info.App}\n.NET：{info.Dotnet}\n系统：{info.Os}\n游戏目录：{info.Root}\n数据目录：{info.Data}"; }), Ui.Button("复制", async () => { if (Owner.Clipboard != null) await Owner.Clipboard.SetTextAsync(output.Text ?? ""); })));
                Body.Children.Add(output);
                break;
            case "about":
                Title("关于");
                Body.Children.Add(Ui.Card(Ui.Stack(
                    Ui.Row(Ui.Logo(64), Ui.Stack(
                        Ui.Heading(MCUGLauncher.ProductInfo.Name),
                        Ui.Text("版本 " + MCUGLauncher.ProductInfo.Version),
                        Ui.Muted("AI 迁移框架尝试 · 测试版本"))),
                    Ui.Text("项目地址：https://github.com/Sconance/MCUGLauncher"),
                    Ui.Action("打开项目仓库", () => System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo("https://github.com/Sconance/MCUGLauncher") { UseShellExecute = true })))));
                break;
        }
    }
    async Task OpenData()
    {
        var info = await Session.SystemInfoAsync();
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(info.Data) { UseShellExecute = true });
    }
    void Install()
    {
        Title("安装与导入", "选择游戏版本安装加载器，或导入本地整合包。");
        Body.Children.Add(Ui.SettingsCard("Minecraft 与加载器", Ui.Text("在版本列表中选择正式版、快照或历史版本，再查看兼容的加载器。"), Ui.Action("选择游戏版本", () => UI.Navigate?.Invoke("vanilla"))));
        Body.Children.Add(Ui.Card(Ui.Stack(Ui.Heading("本地整合包"), Ui.Text("支持的 ZIP / mrpack 会先显示名称与目录确认。"), Ui.Button("选择整合包", () => InstallDialogs.Import(Session, UI, Owner)), Ui.Action("查看任务", () => UI.Navigate?.Invoke("tasks")))));
    }

}
