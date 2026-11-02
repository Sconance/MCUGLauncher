using Avalonia.Controls;
using MCUGLauncher.Avalonia.Presentation;

namespace MCUGLauncher.Avalonia.Views;
internal sealed class SettingsPage : NativePage
{
    public SettingsPage(LauncherFacade session) : base(session)
    {
        Title("启动设置", "保留原配置文件和隐藏设置，不因更换框架重置用户配置。");
        var root = Ui.Input(State.Root);
        var java = Ui.Input(State.Java);
        var memory = Ui.Input(State.Ram.ToString());
        var auto = Ui.Check("自动分配内存", State.AutoMemory);
        var width = Ui.Input(State.Width.ToString());
        var height = Ui.Input(State.Height.ToString());
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
                    Ui.Row(Ui.Field("游戏窗口宽", width), Ui.Field("游戏窗口高", height)),
                    Ui.Button("保存", async () =>
                        {
                            if (!int.TryParse(memory.Text, out var ram) || !int.TryParse(width.Text, out var w) || !int.TryParse(height.Text, out var h))
                                throw new ArgumentException("内存和窗口尺寸必须是整数。");
                            await Send(new
                            {
                                type = "saveSettings",
                                root = root.Text ?? "",
                                java = java.Text ?? "",
                                ram,
                                autoMemory = auto.IsChecked == true,
                                width = w,
                                height = h
                            });
                        }, true),
                    Ui.Button("高级启动设置", () => LaunchOptionsPage.Open(Session, Owner))
        )));
    }
}

internal sealed class JavaPage : NativePage
{
    readonly StackPanel results = Ui.Stack();
    public JavaPage(LauncherFacade session) : base(session)
    {
        Title("Java 管理", "复用 Core 扫描器，启动仍会按实例要求选择 Java。");
        var manual = Ui.Input(State.Java);
        Body.Children.Add(Ui.Card(Ui.Stack(Ui.Field("当前 Java 路径", manual), Ui.Row(Ui.Button("选择", async () => manual.Text = await FilePicker.Open(Owner, "Java", "java.exe", "javaw.exe") ?? manual.Text), Ui.Button("保存", () => Send(new { type = "saveSettings", java = manual.Text ?? "" }))))));
        Body.Children.Add(Ui.Button("扫描 Java", async () =>
                {
                    var response = await Session.RequestAsync("javaList", new
                    {
                        type = "scanJava"
                    });
                    results.Children.Clear();
                    foreach (var item in response.Items)
                        results.Children.Add(Ui.Card(Ui.Stack(
                                    Ui.Text($"Java {item.Major} · {item.Arch} {item.Vendor}"),
                                    Ui.Text(item.Path),
                                    Ui.Text(item.Error),
                                    Ui.Button("设为全局 Java", async () => { manual.Text = item.Path; await Send(new { type = "saveSettings", java = item.Path }); })
                        )));
                    if (response.Items.Length == 0)
                        results.Children.Add(Ui.Text("未发现 Java，请手动选择。"));
                }, true));
        Body.Children.Add(results);
    }
}
