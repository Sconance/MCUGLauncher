using Avalonia.Controls;
using MCUGLauncher.Avalonia.Presentation;

namespace MCUGLauncher.Avalonia.Views;
internal sealed class LaunchOptionsPage : NativePage
{
    public LaunchOptionsPage(LauncherFacade session) : base(session)
    {
        Title("启动参数");
    }

    public async Task Load(string version, bool inline = false)
    {
        var id = Guid.NewGuid().ToString("N");
        var result = await Session.RequestAsync("launchOptions", new
        {
            type = "readLaunchOptions",
            version,
            requestId = id
        }, m => m.RequestId == id);
        var options = result.Settings;
        var name = Ui.Input(result.Name);
        var inherit = Ui.Check("继承全局启动设置", result.Inherited);
        var auto = Ui.Check("自动分配内存", options.AutoMemory);
        var full = Ui.Check("全屏启动", options.FullScreen);
        var chinese = Ui.Check("启动时自动切换为简体中文", options.AutoChinese);
        var java = Ui.Input(options.Java);
        var ram = Ui.Input(options.RamMb.ToString());
        var width = Ui.Input(options.Width.ToString());
        var height = Ui.Input(options.Height.ToString());
        var args = Ui.Input(options.JvmArguments);
        var title = Ui.Input(options.WindowTitle);
        var server = Ui.Input(options.Server);
        Control javaEditor = version.Length > 0
            ? Ui.Stack(Ui.Field("Java（留空自动）", java), Ui.Button("选择 Java", async () => java.Text = await FilePicker.Open(Owner, "Java", "java.exe", "javaw.exe") ?? java.Text))
            : Ui.Setting("Java 运行环境", "由 Java 管理统一选择，默认自动匹配游戏版本", Ui.Action("Java 管理", () => Session.Navigate?.Invoke("java")));
        var fields = Ui.Stack(
            Ui.Text("运行环境与内存", 16), javaEditor, auto,
            Ui.Field("内存 MB", ram),
            Ui.Row(
                Ui.Field("游戏窗口宽", width),
                Ui.Field("游戏窗口高", height)
            ),
            full,
            chinese,
            Ui.Muted("开启后会在启动前设置简体中文；关闭后保留游戏自己的语言设置。运行中的实例不修改。"),
            Ui.Field("附加 JVM 参数", args),
            Ui.Field("窗口标题", title),
            Ui.Field("自动加入服务器", server)
        );
        void Enabled()
        {
            fields.IsEnabled = version.Length == 0 || inherit.IsChecked != true;
            ram.IsEnabled = auto.IsChecked != true;
        }

        inherit.IsCheckedChanged += (_, _) => Enabled();
        auto.IsCheckedChanged += (_, _) => Enabled();
        if (version.Length > 0)
        {
            Body.Children.Add(Ui.Field("实例显示名称", name));
            Body.Children.Add(inherit);
        }

        Body.Children.Add(Ui.Card(fields));
        Enabled();
        var save = Ui.Button("保存设置", async () =>
                {
                    if (!int.TryParse(ram.Text, out var memory) || !int.TryParse(width.Text, out var w) || !int.TryParse(height.Text, out var h))
                        throw new ArgumentException("内存和窗口尺寸必须是整数。");
                    await Send(new
                    {
                        type = "saveLaunchOptions",
                        token = result.Token,
                        name = name.Text ?? "",
                        inherit = inherit.IsChecked == true,
                        settings = new
                        {
                            autoMemory = auto.IsChecked == true,
                            autoChinese = chinese.IsChecked == true,
                            java = java.Text ?? "",
                            ramMb = memory,
                            width = w,
                            height = h,
                            fullScreen = full.IsChecked == true,
                            jvmArguments = args.Text ?? "",
                            windowTitle = title.Text ?? "",
                            server = server.Text ?? ""
                        }
                    });
                }, true);
        if (inline)
            Body.Children.Add(Ui.Row(save));
        else
            DialogActions = Ui.DialogActions(this, save);
    }

    public static async Task Open(LauncherFacade session, Window owner, string version = "")
    {
        var view = new LaunchOptionsPage(session);
        await view.Load(version);
        await Ui.Show(owner, version.Length > 0 ? "单实例启动设置" : "全局启动设置", view);
    }
}
