using Avalonia.Controls;

namespace MCUGLauncher.Avalonia.Views;
internal sealed class JavaPage : NativePage
{
    readonly StackPanel results = Ui.Stack();
    public JavaPage(ILauncherService session, UiContext ui) : base(session, ui)
    {
        Title("Java 管理", "默认自动匹配游戏版本，也可手动指定本机 Java。");
        var manual = Ui.Input(State.Java);
        var choose = Ui.Button("选择 Java", async () =>
            manual.Text = await FilePicker.Open(Owner, "Java", "java.exe", "javaw.exe") ?? manual.Text);
        var save = Ui.Button("保存", () => Session.SaveSettingsAsync(new AppSettingsPatch { Java = manual.Text ?? "" }));
        var automatic = Ui.Button("使用自动选择", () =>
        {
            manual.Text = "";
            return Session.SaveSettingsAsync(new AppSettingsPatch { Java = "" });
        });
        Body.Children.Add(Ui.SettingsCard("默认运行环境",
            Ui.Field("Java 路径（留空自动）", manual), Ui.Row(choose, save, automatic)));
        Body.Children.Add(Ui.Button("扫描 Java", async () =>
                {
                    var response = await Session.ScanJavaAsync();
                    results.Children.Clear();
                    foreach (var item in response.Items)
                        results.Children.Add(Ui.Card(Ui.Stack(
                                    Ui.Text($"Java {item.Major} · {item.Arch} {item.Vendor}"),
                                    Ui.Text(item.Path),
                                    Ui.Text(item.Error),
                                    Ui.Button("设为全局 Java", async () => { manual.Text = item.Path; await Session.SaveSettingsAsync(new AppSettingsPatch { Java = item.Path }); })
                        )));
                    if (response.Items.Length == 0)
                        results.Children.Add(Ui.Text("未发现 Java，请手动选择。"));
                }, true));
        Body.Children.Add(results);
    }
}
