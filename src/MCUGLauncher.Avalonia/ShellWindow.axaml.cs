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
    LauncherFacade facade = null!;
    ShellDesktopHost host = null!;
    string exportVersion = "";
    JsonElement state;
    LauncherState? typed;
    IReadOnlyList<TaskInfo> tasks = Array.Empty<TaskInfo>();
    IReadOnlyList<ContentItem> contentItems = Array.Empty<ContentItem>();
    JsonElement uninstallPreview;
    JsonElement updates;
    JsonElement updatePreview;
    string page = "home";

    public ShellWindow()
    {
        InitializeComponent();
        using (var empty = JsonDocument.Parse("{}"))
            state = empty.RootElement.Clone();
        host = new ShellDesktopHost(
            () => Handle,
            message => facade?.SendMessage(message),
            HandleWindow,
            DragWindow,
            ResetWindow);
        facade = new LauncherFacade(host);
        facade.MessageReceived += OnMessage;
        facade.ContentChanged += list =>
        {
            contentItems = list;
            if (page == "download")
                Render();
        };
        facade.TasksChanged += list =>
        {
            tasks = list;
            if (page == "tasks")
                Render();
        };
        facade.StateChanged += snapshot =>
        {
            typed = snapshot;
            if (page == "home")
                Render();
        };
        HomeButton.Click += (_, _) => Show("home");
        DownloadButton.Click += (_, _) => Show("download");
        MaintenanceButton.Click += (_, _) => Show("maintenance");
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
        if (type.GetString() == "pickedPath"
            && ReadString(message, "target") == "export"
            && exportVersion.Length > 0)
        {
            var version = exportVersion;
            exportVersion = "";
            _ = facade.ExportInstanceAsync(version, ReadString(message, "path"), false, false);
            return;
        }
        if (type.GetString() == "state")
        {
            state = message;
            Render();
        }
        else if (type.GetString() == "uninstallPreview")
        {
            uninstallPreview = message.Clone();
            if (page == "maintenance")
                Render();
        }
        else if (type.GetString() == "contentError")
        {
            Add("下载失败：" + ReadString(message, "message"));
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
        else if (page == "maintenance")
            RenderMaintenance();
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
        var snapshot = typed;
        Add("账号：" + (snapshot?.AccountLabel is { Length: > 0 } label ? label : "离线账号"));
        Add("玩家：" + (snapshot?.Player ?? ""));
        Add("当前版本：" + (snapshot?.SelectedVersion ?? ""));

        AddHeading("实例");
        if (snapshot is { Instances.Count: > 0 })
        {
            foreach (var instance in snapshot.Instances)
            {
                if (instance.Name.Length == 0)
                    continue;
                var row = new Button
                {
                    Content = "启动  " + instance.Name + (instance.Loader.Length > 0 ? "  ·  " + instance.Loader : ""),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left
                };
                row.Click += async (_, _) => await facade.LaunchAsync(instance.Name);
                Content.Children.Add(row);
            }
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
        var snapshot = typed;
        var root = new TextBox { Text = snapshot?.Root ?? "", Watermark = "游戏根目录" };
        var java = new TextBox { Text = snapshot?.Java ?? "", Watermark = "Java 路径（留空自动）" };
        var ram = new TextBox { Text = (snapshot?.Ram ?? 4096).ToString(), Watermark = "内存 MB" };
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

    void RenderMaintenance()
    {
        var snapshot = typed;
        AddHeading("实例维护");
        if (snapshot is { Instances.Count: > 0 })
        {
            var cloneName = new TextBox { Watermark = "新实例名称", Width = 260, HorizontalAlignment = HorizontalAlignment.Left };
            Content.Children.Add(cloneName);
            foreach (var instance in snapshot.Instances)
            {
                if (instance.Name.Length == 0)
                    continue;
                Add(instance.Name);
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                var clone = new Button { Content = "克隆" };
                clone.Click += async (_, _) =>
                {
                    var name = cloneName.Text?.Trim() ?? "";
                    if (name.Length == 0)
                        return;
                    await facade.CloneInstanceAsync(instance.Name, name);
                    await facade.RequestStateAsync();
                };
                var export = new Button { Content = "导出" };
                export.Click += (_, _) => { exportVersion = instance.Name; host.PickSaveFile("export"); };
                var updates = new Button { Content = "检查更新" };
                updates.Click += async (_, _) =>
                {
                    await facade.CheckUpdatesAsync(instance.Name);
                    Add("已提交更新检查：" + instance.Name);
                };
                var uninstall = new Button { Content = "卸载预览" };
                uninstall.Click += async (_, _) => await facade.PreviewUninstallAsync(instance.Name);
                row.Children.Add(clone);
                row.Children.Add(export);
                row.Children.Add(updates);
                row.Children.Add(uninstall);
                Content.Children.Add(row);
            }
        }
        if (uninstallPreview.ValueKind == JsonValueKind.Object
            && uninstallPreview.TryGetProperty("token", out var tokenValue)
            && tokenValue.GetString() is { Length: > 0 } token)
        {
            AddHeading("卸载预览");
            Add(ReadString(uninstallPreview, "notice"));
            if (uninstallPreview.TryGetProperty("paths", out var paths) && paths.ValueKind == JsonValueKind.Array)
                foreach (var path in paths.EnumerateArray())
                    Add("· " + (path.GetString() ?? ""));
            var apply = new Button { Content = "确认卸载", HorizontalAlignment = HorizontalAlignment.Left };
            apply.Click += async (_, _) => { await facade.ApplyUninstallAsync(token); await facade.RequestStateAsync(); };
            Content.Children.Add(apply);
        }
        else
        {
            Add("选择实例后可克隆或预览卸载。卸载预览必须先查看路径。");
        }

        if (updates.ValueKind == JsonValueKind.Object
            && updates.TryGetProperty("items", out var updateItems) && updateItems.ValueKind == JsonValueKind.Array)
        {
            AddHeading("内容更新");
            var projects = new List<string>();
            foreach (var item in updateItems.EnumerateArray())
            {
                var available = item.TryGetProperty("available", out var flag) && flag.ValueKind == JsonValueKind.True;
                Add(ReadString(item, "name") + "  " + ReadString(item, "before") + " → " + ReadString(item, "after"));
                if (available)
                    projects.Add(ReadString(item, "id"));
            }
            if (projects.Count > 0 && updates.TryGetProperty("token", out var checkToken) && checkToken.GetString() is { Length: > 0 } updateToken)
            {
                var preview = new Button { Content = "预览全部更新", HorizontalAlignment = HorizontalAlignment.Left };
                preview.Click += async (_, _) =>
                {
                    await facade.PreviewUpdatesAsync(updateToken, projects);
                    await facade.RequestStateAsync();
                };
                Content.Children.Add(preview);
            }
        }
        if (updatePreview.ValueKind == JsonValueKind.Object
            && updatePreview.TryGetProperty("token", out var previewToken) && previewToken.GetString() is { Length: > 0 } applyToken)
        {
            AddHeading("更新预览");
            Add("预计下载：" + (updatePreview.TryGetProperty("bytes", out var bytes) && bytes.TryGetInt64(out var size) ? size / 1024 + " KB" : "未知"));
            var apply = new Button { Content = "应用更新", HorizontalAlignment = HorizontalAlignment.Left };
            apply.Click += async (_, _) => { await facade.ApplyUpdatesAsync(applyToken); await facade.RequestStateAsync(); };
            Content.Children.Add(apply);
        }
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

        if (contentItems.Count > 0)
        {
            foreach (var item in contentItems)
            {
                var name = item.Name;
                var desc = item.Description;
                Add(name + (desc.Length > 0 ? "  ·  " + desc : ""));
                var install = new Button { Content = "安装 " + name, HorizontalAlignment = HorizontalAlignment.Left };
                var projectId = item.Id;
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
        var snapshot = typed;
        AddHeading("账号");
        Add("当前：" + (snapshot?.AccountLabel is { Length: > 0 } label ? label : "离线账号"));
        Add("玩家：" + (snapshot?.Player ?? ""));
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
            await facade.ForgetAccountAsync(snapshot?.AccountType is { Length: > 0 } mode ? mode : "offline");
            await facade.RequestStateAsync();
        };
        Content.Children.Add(forget);
    }

    void RenderTasks()
    {
        AddHeading("下载任务");
        if (tasks.Count == 0)
        {
            Add("当前没有排队或运行中的任务。");
            return;
        }
        foreach (var task in tasks)
        {
            Add($"{task.Name} · {task.State} · {task.Percent:0}%");
            if (task.Message.Length > 0)
                Add("  " + task.Message);
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            foreach (var (label, action) in new[]
                     {
                         ("暂停", "pause"), ("继续", "resume"), ("取消", "cancel"), ("删除", "delete")
                     })
            {
                var button = new Button { Content = label };
                var taskId = task.Id;
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

    IntPtr Handle => TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;

    void HandleWindow(string action)
    {
        switch (action)
        {
            case "minimize":
                WindowState = WindowState.Minimized;
                break;
            case "maximize":
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                break;
            case "close":
                Close();
                break;
        }
    }

    void DragWindow()
    {
        if (Handle == IntPtr.Zero)
            return;
        ReleaseCapture();
        SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
    }

    void ResetWindow()
    {
        Width = 1100;
        Height = 720;
        WindowState = WindowState.Normal;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool ReleaseCapture();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);

    void AddHeading(string text) =>
        Content.Children.Add(new TextBlock { Text = text, FontSize = 18, FontWeight = FontWeight.Bold, Margin = new Thickness(0, 12, 0, 0) });

    void Add(string text) =>
        Content.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap });

    static string ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
}
