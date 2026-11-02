using Avalonia.Controls;
using MCUGLauncher.Avalonia.Presentation;

namespace MCUGLauncher.Avalonia.Views;
internal sealed class SettingsPage : NativePage
{
    public SettingsPage(LauncherFacade session) : base(session)
    {
        Title("启动与环境", "保留原配置文件和隐藏设置，不因更换框架重置用户配置。");
        var root = Ui.Input(State.Root);
        var java = Ui.Input(State.Java);
        var memory = Ui.Input(State.Ram.ToString());
        var auto = Ui.Check("自动分配内存", State.AutoMemory);
        var anon = Ui.Check("匿名模式", State.Anonymous);
        auto.IsCheckedChanged += (_, _) => memory.IsEnabled = auto.IsChecked != true;
        memory.IsEnabled = !State.AutoMemory;
        Body.Children.Add(Ui.Card(Ui.Stack(
                    Ui.Field("游戏根目录", root),
                    Ui.Button("选择目录", async () => root.Text = await FilePicker.Folder(Owner) ?? root.Text),
                    Ui.Field("Java（留空自动）", java),
                    Ui.Row(
                        Ui.Button("选择 Java", async () => java.Text = await FilePicker.Open(Owner, "Java", "java.exe", "javaw.exe") ?? java.Text),
                        Ui.Action("Java 管理", () => Session.Navigate?.Invoke("java"))
                    ),
                    auto,
                    Ui.Field("手动内存 MB", memory),
                    anon,
                    Ui.Button("保存", async () =>
                        {
                            if (!int.TryParse(memory.Text, out var ram))
                                throw new ArgumentException("内存必须是整数。");
                            await Send(new
                            {
                                type = "saveSettings",
                                root = root.Text ?? "",
                                java = java.Text ?? "",
                                ram,
                                autoMemory = auto.IsChecked == true,
                                anonymous = anon.IsChecked == true
                            });
                        }, true),
                    Ui.Button("完整全局启动参数", () => LaunchOptionsPage.Open(Session, Owner)),
                    Ui.Button("恢复默认窗口尺寸", () => Send(new { type = "resetWindow" }))
        )));
    }
}

internal sealed class JavaPage : NativePage
{
    readonly StackPanel results = Ui.Stack();
    public JavaPage(LauncherFacade session) : base(session)
    {
        Title("Java 管理", "复用 Core 扫描器，启动仍会按实例要求选择 Java。");
        Body.Children.Add(Ui.Button("扫描 Java", async () =>
                {
                    var response = await Session.RequestAsync("javaList", new
                    {
                        type = "scanJava"
                    });
                    results.Children.Clear();
                    foreach (var item in response.Items)
                        results.Children.Add(Ui.Card(Ui.Stack(
                                    Ui.Text(item.Path),
                                    Ui.Text(item.Error),
                                    Ui.Button("设为全局 Java", () => Send(new { type = "saveSettings", java = item.Path }))
                        )));
                    if (response.Items.Length == 0)
                        results.Children.Add(Ui.Text("未发现 Java，请手动选择。"));
                }, true));
        Body.Children.Add(results);
    }
}

internal sealed class DiagnosticsPage : NativePage
{
    readonly TextBox output = Ui.Input();
    public DiagnosticsPage(LauncherFacade session) : base(session)
    {
        Title("诊断与关于", BlockLaunch.ProductInfo.Version + " · AI 迁移框架尝试。Windows 原生 Avalonia 界面，非稳定发布。");
        output.AcceptsReturn = true;
        output.IsReadOnly = true;
        output.MinHeight = 400;
        output.TextWrapping = global::Avalonia.Media.TextWrapping.Wrap;
        Body.Children.Add(Ui.Row(
                Ui.Button("系统信息", async () =>
                    {
                        var info = await Session.RequestAsync("systemInfo", new
                        {
                            type = "systemInfo"
                        });
                        output.Text = $"版本：{info.App}\n.NET：{info.Dotnet}\n系统：{info.Os}\n游戏：{info.Root}\n数据：{info.Data}\n界面：Avalonia 原生控件";
                    }),
                Ui.Button("读取日志", async () => output.Text = (await Session.RequestAsync("logText", new { type = "readLog" })).Text)
        ));
        Body.Children.Add(Ui.Button("第三方许可", async () =>
                {
                    using var stream = global::Avalonia.Platform.AssetLoader.Open(new Uri("avares://MCUGLauncher/Assets/Notices.txt"));
                    using var reader = new StreamReader(stream);
                    output.Text = await reader.ReadToEndAsync();
                }));
        Body.Children.Add(output);
    }
}
