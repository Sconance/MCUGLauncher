using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace MCUGLauncher.Avalonia;

/// <summary>原生 Avalonia 外壳：阶段 1 提供启动/设置两页，旧 WebView 作为回退入口保留。</summary>
public partial class ShellWindow : Window
{
    readonly LauncherFacade facade;
    JsonElement state;
    string page = "home";

    public ShellWindow()
    {
        InitializeComponent();
        using (var empty = JsonDocument.Parse("{}"))
            state = empty.RootElement.Clone();
        facade = new LauncherFacade(new ShellDesktopHost());
        facade.MessageReceived += OnMessage;
        HomeButton.Click += (_, _) => Show("home");
        SettingsButton.Click += (_, _) => Show("settings");
        ClassicButton.Click += (_, _) => new MainWindow().Show();
        Opened += async (_, _) => await facade.StartAsync();
        Closed += (_, _) => facade.Dispose();
    }

    void Show(string next)
    {
        page = next;
        Render();
    }

    void OnMessage(JsonElement message)
    {
        if (!message.TryGetProperty("type", out var type))
            return;
        if (type.GetString() == "state")
        {
            state = message;
            Render();
        }
        else if (type.GetString() == "launchResult")
        {
            var ok = message.TryGetProperty("ok", out var value) && value.ValueKind == JsonValueKind.True;
            Add(ok ? "已启动游戏。" : "启动失败：" + ReadString(message, "message"));
        }
    }

    void Render()
    {
        Content.Children.Clear();
        if (page == "settings")
            RenderSettings();
        else
            RenderHome();
    }

    void RenderHome()
    {
        Add("账号：" + (ReadString(state, "accountLabel") is { Length: > 0 } label ? label : "离线账号"));
        Add("玩家：" + ReadString(state, "player"));
        Add("当前版本：" + ReadString(state, "selectedVersion"));

        AddHeading("实例");
        if (state.TryGetProperty("instances", out var instances) && instances.ValueKind == JsonValueKind.Array)
        {
            var any = false;
            foreach (var item in instances.EnumerateArray())
            {
                any = true;
                var name = ReadString(item, "name");
                if (name.Length == 0)
                    continue;
                var loader = ReadString(item, "loader");
                var row = new Button
                {
                    Content = "启动  " + name + (loader.Length > 0 ? "  ·  " + loader : ""),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left
                };
                row.Click += async (_, _) => await facade.LaunchAsync(name);
                Content.Children.Add(row);
            }
            if (!any)
                Add("尚未找到已安装实例。");
        }
        else
        {
            Add("尚未找到已安装实例。");
        }

        var refresh = new Button { Content = "刷新状态", HorizontalAlignment = HorizontalAlignment.Left };
        refresh.Click += async (_, _) => await facade.RequestStateAsync();
        Content.Children.Add(refresh);
    }

    void RenderSettings()
    {
        AddHeading("设置");
        Add("游戏目录：" + ReadString(state, "root"));
        Add("Java：" + (ReadString(state, "java") is { Length: > 0 } java ? java : "自动"));
        Add("内存：" + (state.TryGetProperty("ram", out var ram) && ram.TryGetInt32(out var value) ? value + " MB" : "默认"));
        Add("说明：设置页的原生编辑将在阶段 2 接入，当前可通过“经典界面”修改。");
    }

    void AddHeading(string text) =>
        Content.Children.Add(new TextBlock { Text = text, FontSize = 18, FontWeight = FontWeight.Bold, Margin = new Thickness(0, 12, 0, 0) });

    void Add(string text) =>
        Content.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap });

    static string ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
}
