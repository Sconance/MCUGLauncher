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
    JsonElement tasks;
    JsonElement contentItems;
    string page = "home";

    public ShellWindow()
    {
        InitializeComponent();
        using (var empty = JsonDocument.Parse("{}"))
            state = empty.RootElement.Clone();
        facade = new LauncherFacade(new ShellDesktopHost());
        facade.MessageReceived += OnMessage;
        HomeButton.Click += (_, _) => Show("home");
        DownloadButton.Click += (_, _) => Show("download");
        AccountButton.Click += (_, _) => Show("account");
        TasksButton.Click += (_, _) => { page = "tasks"; _ = facade.RequestTasksAsync(); Render(); };
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
        else if (type.GetString() == "contentList")
        {
            if (message.TryGetProperty("items", out var items))
                contentItems = items.Clone();
            if (page == "download")
                Render();
        }
        else if (type.GetString() == "contentError")
        {
            Add("下载失败：" + ReadString(message, "message"));
        }
        else if (type.GetString() == "tasks")
        {
            if (message.TryGetProperty("items", out var items))
                tasks = items.Clone();
            if (page == "tasks")
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
        else if (page == "download")
            RenderDownload();
        else if (page == "account")
            RenderAccount();
        else if (page == "tasks")
            RenderTasks();
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
        var root = new TextBox { Text = ReadString(state, "root"), Watermark = "游戏根目录" };
        var java = new TextBox { Text = ReadString(state, "java"), Watermark = "Java 路径（留空自动）" };
        var ram = new TextBox
        {
            Text = state.TryGetProperty("ram", out var ramValue) && ramValue.TryGetInt32(out var value) ? value.ToString() : "4096",
            Watermark = "内存 MB"
        };
        Add("游戏目录");
        Content.Children.Add(root);
        Add("Java");
        Content.Children.Add(java);
        Add("内存（MB）");
        Content.Children.Add(ram);

        var save = new Button { Content = "保存设置", HorizontalAlignment = HorizontalAlignment.Left };
        save.Click += async (_, _) =>
        {
            var memory = int.TryParse(ram.Text, out var parsed) ? Math.Clamp(parsed, 512, 65536) : 4096;
            await facade.SaveSettingsAsync(root.Text?.Trim() ?? "", java.Text?.Trim() ?? "", memory);
            await facade.RequestStateAsync();
            Add("设置已保存。");
        };
        Content.Children.Add(save);
        Add("提示：目录与 Java 的浏览对话框将在下一阶段接入。");
    }

    void RenderDownload()
    {
        AddHeading("下载");
        var category = new ComboBox
        {
            ItemsSource = new[] { "mod", "modpack", "resourcepack", "shader", "datapack" },
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        var query = new TextBox { Watermark = "搜索关键词", Width = 320, HorizontalAlignment = HorizontalAlignment.Left };
        Content.Children.Add(category);
        Content.Children.Add(query);
        var search = new Button { Content = "搜索", HorizontalAlignment = HorizontalAlignment.Left };
        search.Click += async (_, _) =>
            await facade.SearchContentAsync(category.SelectedItem as string ?? "mod", query.Text?.Trim() ?? "");
        Content.Children.Add(search);

        if (contentItems.ValueKind == JsonValueKind.Array && contentItems.GetArrayLength() > 0)
        {
            foreach (var item in contentItems.EnumerateArray())
            {
                var id = ReadString(item, "id");
                var name = ReadString(item, "name");
                var desc = ReadString(item, "description");
                Add(name + (desc.Length > 0 ? "  ·  " + desc : ""));
                var install = new Button { Content = "安装 " + name, HorizontalAlignment = HorizontalAlignment.Left };
                var projectId = id;
                var type = category.SelectedItem as string ?? "mod";
                install.Click += async (_, _) =>
                {
                    await facade.InstallContentAsync(projectId, type);
                    await facade.RequestStateAsync();
                };
                Content.Children.Add(install);
            }
        }
        else
        {
            Add("输入关键词后搜索 Modrinth。");
        }
    }

    void RenderAccount()
    {
        AddHeading("账号");
        Add("当前：" + (ReadString(state, "accountLabel") is { Length: > 0 } label ? label : "离线账号"));
        Add("玩家：" + ReadString(state, "player"));

        var microsoft = new Button { Content = "登录微软账号", HorizontalAlignment = HorizontalAlignment.Left };
        microsoft.Click += async (_, _) => { await facade.LoginMicrosoftAsync(); await facade.RequestStateAsync(); };
        Content.Children.Add(microsoft);

        AddHeading("MUA 账号");
        var account = new TextBox { Watermark = "MUA 账号" };
        var password = new TextBox { Watermark = "密码", PasswordChar = '●' };
        Content.Children.Add(account);
        Content.Children.Add(password);
        var muaLogin = new Button { Content = "登录 MUA", HorizontalAlignment = HorizontalAlignment.Left };
        muaLogin.Click += async (_, _) =>
        {
            await facade.LoginMuaAsync(account.Text?.Trim() ?? "", password.Text ?? "");
            await facade.RequestStateAsync();
        };
        Content.Children.Add(muaLogin);

        var offline = new Button { Content = "切换到离线账号", HorizontalAlignment = HorizontalAlignment.Left };
        offline.Click += async (_, _) => { await facade.SwitchAccountModeAsync("offline"); await facade.RequestStateAsync(); };
        Content.Children.Add(offline);

        var forget = new Button { Content = "忘记已保存账号", HorizontalAlignment = HorizontalAlignment.Left };
        forget.Click += async (_, _) =>
        {
            await facade.ForgetAccountAsync(ReadString(state, "accountType") is { Length: > 0 } mode ? mode : "offline");
            await facade.RequestStateAsync();
        };
        Content.Children.Add(forget);
    }

    void RenderTasks()
    {
        AddHeading("下载任务");
        if (tasks.ValueKind != JsonValueKind.Array || tasks.GetArrayLength() == 0)
        {
            Add("当前没有排队或运行中的任务。");
            return;
        }
        foreach (var task in tasks.EnumerateArray())
        {
            var id = ReadString(task, "id");
            var name = ReadString(task, "name");
            var stateText = ReadString(task, "state");
            var percent = task.TryGetProperty("percent", out var p) && p.TryGetDouble(out var value) ? value : 0;
            var message = ReadString(task, "message");
            Add($"{name} · {stateText} · {percent:0}%");
            if (message.Length > 0)
                Add("  " + message);
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            foreach (var (label, action) in new[]
                     {
                         ("暂停", "pause"), ("继续", "resume"), ("取消", "cancel"), ("删除", "delete")
                     })
            {
                var button = new Button { Content = label };
                var taskId = id;
                var taskAction = action;
                button.Click += async (_, _) =>
                {
                    await facade.TaskActionAsync(taskId, taskAction);
                    await facade.RequestTasksAsync();
                };
                row.Children.Add(button);
            }
            Content.Children.Add(row);
        }
        var refresh = new Button { Content = "刷新任务", HorizontalAlignment = HorizontalAlignment.Left };
        refresh.Click += async (_, _) => await facade.RequestTasksAsync();
        Content.Children.Add(refresh);
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
