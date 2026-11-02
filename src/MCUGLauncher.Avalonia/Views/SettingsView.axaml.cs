using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace MCUGLauncher.Avalonia.Views;

internal partial class SettingsView : UserControl
{
    readonly Dictionary<string, Control> panels = new();
    readonly Dictionary<string, string> pings = new(StringComparer.OrdinalIgnoreCase);
    LauncherFacade? facade;
    LauncherState? state;
    string current = "launch";
    string editingServer = "";
    bool updatingRoots;
    bool addRootOnPick;

    public SettingsView()
    {
        InitializeComponent();
        panels["launch"] = LaunchPanel;
        panels["java"] = JavaPanel;
        panels["datasources"] = DataSourcesPanel;
        panels["appearance"] = AppearancePanel;
        panels["servers"] = ServersPanel;
        panels["saves"] = SavesPanel;
        panels["recovery"] = RecoveryPanel;
        panels["logs"] = LogsPanel;
        panels["diagnose"] = DiagnosePanel;
        panels["community"] = CommunityPanel;
        panels["about"] = AboutPanel;
        WireButtons();
        JavaListPanel.Children.Add(Muted("还没有扫描结果。点击“扫描本机 Java”。"));
        SaveListPanel.Children.Add(Muted("还没有存档备份。"));
        ServerListPanel.Children.Add(Muted("还没有收藏服务器。"));
        Nav.SelectedIndex = 0;
    }

    public SettingsView(LauncherFacade facade) : this()
    {
        Attach(facade);
    }

    public void Attach(LauncherFacade value)
    {
        if (facade is not null)
            Detach();
        facade = value;
        facade.StateChanged += ApplyState;
        facade.JavaListChanged += RenderJavaList;
        facade.SaveBackupsChanged += RenderSaveBackups;
        facade.LogTextReceived += text => LogBox.Text = text;
        facade.SystemInfoReceived += info => DiagnosticsBox.Text = Format(info);
        facade.PathPicked += OnPathPicked;
        facade.ServerPingReceived += OnServerPing;
        facade.ServerResultReceived += OnServerResult;
        facade.TestResultReceived += OnTestResult;
        facade.KeyResultReceived += OnKeyResult;
        facade.ToastReceived += info => Status(info.Message);
        facade.ErrorReceived += message => Status("操作失败：" + message);
        facade.SettingsSaved += () => Status("设置已保存。");
        ApplyState(facade.State);
    }

    void Detach()
    {
        if (facade is null)
            return;
        facade.StateChanged -= ApplyState;
        facade.JavaListChanged -= RenderJavaList;
        facade.SaveBackupsChanged -= RenderSaveBackups;
        facade.PathPicked -= OnPathPicked;
        facade.ServerPingReceived -= OnServerPing;
        facade.ServerResultReceived -= OnServerResult;
        facade.TestResultReceived -= OnTestResult;
        facade.KeyResultReceived -= OnKeyResult;
        facade = null;
    }

    void WireButtons()
    {
        BrowseRootButton.Click += async (_, _) =>
        {
            addRootOnPick = false;
            await Pick(facade?.PickFolderAsync("root"));
        };
        AddRootButton.Click += async (_, _) =>
        {
            addRootOnPick = true;
            await Pick(facade?.PickFolderAsync("root"));
        };
        GameRootBox.SelectionChanged += async (_, _) =>
        {
            if (updatingRoots || facade is null || GameRootBox.SelectedItem is not string root)
                return;
            if (root.Length == 0 || root == state?.Root)
                return;
            LaunchStatus.Text = "正在切换游戏目录…";
            await facade.SaveSettingsAsync(new AppSettingsPatch(Root: root));
        };
        BrowseJavaButton.Click += async (_, _) => await Pick(facade?.PickFileAsync("java"));
        ScanJavaButton.Click += async (_, _) => await ScanJava(JavaStatus);
        SaveLaunchButton.Click += async (_, _) => await SaveLaunch();
        BrowseJavaPathButton.Click += async (_, _) => await Pick(facade?.PickFileAsync("java"));
        ScanJavaListButton.Click += async (_, _) => await ScanJava(JavaStatus);
        SaveJavaButton.Click += async (_, _) => await SaveJava();
        TestAnnouncementsButton.Click += async (_, _) => await TestSource("公告", SourceAnnouncementsBox, "announcements");
        TestServersButton.Click += async (_, _) => await TestSource("服务器", SourceServersBox, "servers");
        TestRecommendationsButton.Click += async (_, _) => await TestSource("推荐", SourceRecommendationsBox, "recommendations");
        TestQuotesButton.Click += async (_, _) => await TestSource("语录", SourceQuotesBox, "quotes");
        SaveSourcesButton.Click += async (_, _) => await SaveSources(use: false);
        UseCustomSourcesButton.Click += async (_, _) => await SaveSources(use: true);
        UseDefaultSourcesButton.Click += async (_, _) => await UseDefaultSources();
        SaveWriteEndpointButton.Click += async (_, _) => await SaveWriteEndpoint();
        ResetWindowButton.Click += async (_, _) => await facade!.ResetWindowAsync();
        SaveServerButton.Click += async (_, _) => await SaveServer();
        CancelServerEditButton.Click += (_, _) => ResetServerForm();
        BackupSavesButton.Click += async (_, _) => await facade!.BackupSavesAsync();
        OpenSavesFolderButton.Click += async (_, _) => await facade!.OpenFolderAsync("saves");
        ResetWindowRecoveryButton.Click += async (_, _) => await facade!.ResetWindowAsync();
        OpenConfigButton.Click += async (_, _) => await facade!.OpenFolderAsync("config");
        OpenRootButton.Click += async (_, _) => await facade!.OpenFolderAsync("root");
        ReadLogButton.Click += async (_, _) => await facade!.ReadLogAsync();
        OpenLogFolderButton.Click += async (_, _) => await facade!.OpenFolderAsync("config");
        ReadSystemInfoButton.Click += async (_, _) => await facade!.SystemInfoAsync();
        CopyDiagnosticsButton.Click += async (_, _) => await CopyDiagnostics();
        SaveAnonymousButton.Click += async (_, _) => await SaveAnonymous();
        VerifyCommunityKeyButton.Click += async (_, _) => await VerifyKey("community", CommunityKeyBox);
        VerifyAdminKeyButton.Click += async (_, _) => await VerifyKey("admin", AdminKeyBox);
    }

    static async Task Pick(Task? pending)
    {
        if (pending is not null)
            await pending;
    }

    async Task ScanJava(TextBlock status)
    {
        if (facade is null)
            return;
        status.Text = "正在扫描本机 Java…";
        await facade.ScanJavaAsync();
    }

    async Task SaveLaunch()
    {
        if (facade is null)
            return;
        if (!int.TryParse(RamBox.Text, out var ram) || ram is < 512 or > 65536)
        {
            LaunchStatus.Text = "最大内存需在 512–65536 MB 之间。";
            return;
        }
        if (!int.TryParse(WidthBox.Text, out var width) || width is < 640 or > 7680)
        {
            LaunchStatus.Text = "窗口宽需在 640–7680 之间。";
            return;
        }
        if (!int.TryParse(HeightBox.Text, out var height) || height is < 480 or > 4320)
        {
            LaunchStatus.Text = "窗口高需在 480–4320 之间。";
            return;
        }
        var root = RootBox.Text?.Trim() ?? "";
        if (root.Length == 0)
        {
            LaunchStatus.Text = "请选择已有游戏目录。";
            return;
        }
        var player = PlayerBox.Text?.Trim() ?? "";
        if (state?.AccountType == "offline" && player.Length == 0)
        {
            LaunchStatus.Text = "离线账号需要填写玩家名。";
            return;
        }
        LaunchStatus.Text = "正在保存…";
        await facade.SaveSettingsAsync(new AppSettingsPatch(
            Root: root,
            Java: JavaBox.Text?.Trim() ?? "",
            Ram: ram,
            Width: width,
            Height: height,
            AutoMemory: AutoMemoryBox.IsChecked == true,
            Player: state?.AccountType == "offline" ? player : null));
    }

    async Task SaveJava()
    {
        if (facade is null)
            return;
        JavaStatus.Text = "正在保存 Java 设置…";
        await facade.SaveSettingsAsync(new AppSettingsPatch(Java: JavaPathBox.Text?.Trim() ?? ""));
    }

    async Task TestSource(string label, TextBox box, string requestId)
    {
        if (facade is null)
            return;
        var url = box.Text?.Trim() ?? "";
        if (url.Length == 0)
        {
            DataSourceStatus.Text = label + "地址为空。";
            return;
        }
        DataSourceStatus.Text = "正在测试" + label + "…";
        await facade.TestSourceAsync(label, url, requestId);
    }

    async Task SaveSources(bool use)
    {
        if (facade is null)
            return;
        await facade.SaveCustomSourcesAsync(ReadSources());
        if (use)
        {
            await facade.UseDataSourcesAsync("custom");
            DataSourceStatus.Text = "已保存并切换到自定义数据源。";
        }
        else
            DataSourceStatus.Text = "已保存自定义数据源，可点击“使用自定义”切换。";
    }

    async Task UseDefaultSources()
    {
        if (facade is null)
            return;
        await facade.UseDataSourcesAsync("default");
        DataSourceStatus.Text = "已切换到默认数据源。";
    }

    async Task SaveWriteEndpoint()
    {
        if (facade is null)
            return;
        await facade.SaveSettingsAsync(new AppSettingsPatch(WriteEndpoint: WriteEndpointBox.Text?.Trim() ?? ""));
        DataSourceStatus.Text = "写入接口已保存。";
    }

    async Task SaveServer()
    {
        if (facade is null)
            return;
        var address = ServerAddressBox.Text?.Trim() ?? "";
        if (address.Length == 0)
        {
            ServerStatus.Text = "请填写服务器地址。";
            return;
        }
        ServerStatus.Text = "正在保存…";
        await facade.SaveServerAsync(ServerNameBox.Text?.Trim() ?? "", address, editingServer);
    }

    void ResetServerForm()
    {
        editingServer = "";
        ServerNameBox.Text = "";
        ServerAddressBox.Text = "";
        SaveServerButton.Content = "添加";
        CancelServerEditButton.IsVisible = false;
        ServerStatus.Text = "";
    }

    void BeginEdit(SavedServerInfo server)
    {
        editingServer = server.Address;
        ServerNameBox.Text = server.Name;
        ServerAddressBox.Text = server.Address;
        SaveServerButton.Content = "更新";
        CancelServerEditButton.IsVisible = true;
        ServerStatus.Text = "正在编辑 " + server.Address;
    }

    async Task SaveAnonymous()
    {
        if (facade is null)
            return;
        await facade.SaveSettingsAsync(new AppSettingsPatch(Anonymous: AnonymousBox.IsChecked == true));
        CommunityStatus.Text = "匿名设置已保存。";
    }

    async Task VerifyKey(string role, TextBox box)
    {
        if (facade is null)
            return;
        var key = box.Text?.Trim() ?? "";
        if (key.Length == 0)
        {
            CommunityStatus.Text = "请输入密钥。";
            return;
        }
        CommunityStatus.Text = "正在验证…";
        await facade.VerifyKeyAsync(role, key);
    }

    async Task CopyDiagnostics()
    {
        var text = DiagnosticsBox.Text ?? "";
        if (text.Length == 0)
        {
            DiagnoseStatus.Text = "请先读取系统信息。";
            return;
        }
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            DiagnoseStatus.Text = "剪贴板不可用。";
            return;
        }
        await clipboard.SetTextAsync(text);
        DiagnoseStatus.Text = "诊断信息已复制。";
    }

    void OnNavSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Nav.SelectedItem is ListBoxItem { Tag: string key })
            ShowPanel(key);
    }

    void ShowPanel(string key)
    {
        current = key;
        foreach (var pair in panels)
            pair.Value.IsVisible = pair.Key == key;
        if (key == "saves")
            _ = facade?.ListSaveBackupsAsync();
        else if (key == "logs")
            _ = facade?.ReadLogAsync();
        else if (key == "diagnose")
            _ = facade?.SystemInfoAsync();
    }

    void OnPathPicked(PickedPathInfo info)
    {
        if (info.Target == "root")
        {
            RootBox.Text = info.Path;
            if (addRootOnPick)
            {
                addRootOnPick = false;
                LaunchStatus.Text = "正在添加并切换游戏目录…";
                _ = facade!.SaveSettingsAsync(new AppSettingsPatch(Root: info.Path));
            }
            else
                LaunchStatus.Text = "已选择游戏目录，保存后生效。";
        }
        else if (info.Target == "java")
        {
            JavaBox.Text = info.Path;
            JavaPathBox.Text = info.Path;
            JavaStatus.Text = "已选择 Java 路径，保存后生效。";
        }
    }

    void OnServerPing(ServerPingInfo info)
    {
        pings[info.Id] = info.Online
            ? $"{info.Players}/{info.Max} 人 · {info.Ping} ms · {info.Motd}"
            : "离线 · " + info.Error;
        RenderServers();
    }

    void OnServerResult(ServerResultInfo info)
    {
        ServerStatus.Text = info.Message;
        if (info.Ok)
            ResetServerForm();
    }

    void OnTestResult(TestResultInfo info)
    {
        DataSourceStatus.Text = $"{info.Name}：{(info.Ok ? "可用" : "失败")} · {info.Message} · {info.Ms} ms";
    }

    void OnKeyResult(KeyResultInfo info)
    {
        CommunityStatus.Text = info.Message;
    }

    void Status(string message)
    {
        switch (current)
        {
            case "launch":
                LaunchStatus.Text = message;
                break;
            case "java":
                JavaStatus.Text = message;
                break;
            case "datasources":
                DataSourceStatus.Text = message;
                break;
            case "servers":
                ServerStatus.Text = message;
                break;
            case "saves":
                SaveStatus.Text = message;
                break;
            case "recovery":
                RecoveryStatus.Text = message;
                break;
            case "diagnose":
                DiagnoseStatus.Text = message;
                break;
            case "community":
                CommunityStatus.Text = message;
                break;
        }
    }

    void ApplyState(LauncherState? snapshot)
    {
        state = snapshot;
        if (snapshot is null)
            return;
        SetText(RootBox, snapshot.Root);
        var roots = snapshot.GameRoots
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        updatingRoots = true;
        GameRootBox.ItemsSource = roots;
        GameRootBox.SelectedIndex = Array.IndexOf(roots, snapshot.Root);
        updatingRoots = false;
        SetText(JavaBox, snapshot.Java);
        SetText(JavaPathBox, snapshot.Java);
        SetText(RamBox, snapshot.Ram.ToString());
        SetText(WidthBox, snapshot.Width.ToString());
        SetText(HeightBox, snapshot.Height.ToString());
        SetText(PlayerBox, snapshot.Player);
        SetText(WriteEndpointBox, snapshot.WriteEndpoint);
        SetText(AboutVersion, "MCUG Launcher " + snapshot.AppVersion);
        if (!AutoMemoryBox.IsFocused)
            AutoMemoryBox.IsChecked = snapshot.AutoMemory;
        if (!AnonymousBox.IsFocused)
            AnonymousBox.IsChecked = snapshot.Anonymous;
        PlayerBox.IsEnabled = snapshot.AccountType == "offline";
        PlayerLabel.Text = snapshot.AccountType == "offline" ? "离线玩家名" : "玩家名（在线账号不可修改）";
        var sources = snapshot.CustomDataSources.Announcements.Length > 0
            ? snapshot.CustomDataSources
            : snapshot.DataSources;
        SetText(SourceAnnouncementsBox, sources.Announcements);
        SetText(SourceServersBox, sources.Servers);
        SetText(SourceRecommendationsBox, sources.Recommendations);
        SetText(SourceQuotesBox, sources.Quotes);
        var mode = snapshot.DataSourceMode == "custom" ? "自定义" : "默认";
        var keys = $"身份密钥：{(snapshot.HasCommunityKey ? "已设置" : "未设置")} · 管理员密钥：{(snapshot.HasAdminKey ? "已设置" : "未设置")}";
        if (current == "datasources")
            DataSourceStatus.Text = $"当前使用{mode}数据源。";
        if (current == "community")
            CommunityStatus.Text = keys;
        RenderServers();
        if (facade is { JavaList.Count: > 0 })
            RenderJavaList(facade.JavaList);
        if (facade is { SaveBackups.Count: > 0 })
            RenderSaveBackups(facade.SaveBackups);
    }

    void RenderJavaList(IReadOnlyList<JavaInfo> items)
    {
        JavaListPanel.Children.Clear();
        JavaStatus.Text = items.Count == 0 ? "没有找到可用的 Java。" : $"找到 {items.Count} 个 Java。";
        if (items.Count == 0)
        {
            JavaListPanel.Children.Add(Muted("还没有扫描结果。点击“扫描本机 Java”。"));
            return;
        }
        foreach (var item in items)
        {
            var copy = new StackPanel { Spacing = 2 };
            copy.Children.Add(new TextBlock
            {
                Text = $"Java {item.Major} · {item.Arch} {item.Vendor}".Trim(),
                FontWeight = FontWeight.SemiBold
            });
            copy.Children.Add(new TextBlock { Text = item.Path, Classes = { "muted" }, TextWrapping = TextWrapping.Wrap });
            if (item.Error.Length > 0)
                copy.Children.Add(new TextBlock { Text = item.Error, Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap });
            var use = new Button { Content = "使用", Classes = { "ghost" }, VerticalAlignment = VerticalAlignment.Center };
            var path = item.Path;
            use.Click += (_, _) =>
            {
                JavaPathBox.Text = path;
                JavaBox.Text = path;
                JavaStatus.Text = "已填入 Java 路径，保存后生效。";
            };
            JavaListPanel.Children.Add(Row(copy, use));
        }
    }

    void RenderSaveBackups(IReadOnlyList<SaveBackupInfo> items)
    {
        SaveListPanel.Children.Clear();
        if (items.Count == 0)
        {
            SaveListPanel.Children.Add(Muted("还没有存档备份。"));
            return;
        }
        foreach (var backup in items)
        {
            var copy = new StackPanel { Spacing = 4 };
            copy.Children.Add(new TextBlock
            {
                Text = $"{backup.Created} · {backup.Worlds.Count} 个存档",
                FontWeight = FontWeight.SemiBold
            });
            foreach (var world in backup.Worlds)
            {
                var restore = new Button { Content = "恢复为 " + world, Classes = { "ghost" }, HorizontalAlignment = HorizontalAlignment.Left };
                var id = backup.Id;
                var name = world;
                restore.Click += async (_, _) =>
                {
                    SaveStatus.Text = "正在恢复存档…";
                    await facade!.RestoreSaveAsync(id, name);
                };
                copy.Children.Add(restore);
            }
            SaveListPanel.Children.Add(copy);
        }
    }

    void RenderServers()
    {
        ServerListPanel.Children.Clear();
        if (state is not { Servers.Count: > 0 })
        {
            ServerListPanel.Children.Add(Muted("还没有收藏服务器。"));
            return;
        }
        foreach (var server in state.Servers)
        {
            var copy = new StackPanel { Spacing = 2 };
            copy.Children.Add(new TextBlock
            {
                Text = server.Name.Length > 0 ? server.Name : server.Address,
                FontWeight = FontWeight.SemiBold
            });
            copy.Children.Add(new TextBlock { Text = server.Address, Classes = { "muted" } });
            copy.Children.Add(new TextBlock
            {
                Text = pings.TryGetValue(server.Address, out var status) ? status : "尚未查询",
                Classes = { "muted" }
            });
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            var current = server;
            var ping = new Button { Content = "查询", Classes = { "ghost" } };
            ping.Click += async (_, _) =>
            {
                ServerStatus.Text = "正在查询 " + current.Address + "…";
                await facade!.PingServerAsync(current.Address, current.Address);
            };
            var edit = new Button { Content = "编辑", Classes = { "ghost" } };
            edit.Click += (_, _) => BeginEdit(current);
            var delete = new Button { Content = "删除", Classes = { "ghost" } };
            delete.Click += async (_, _) =>
            {
                await facade!.DeleteServerAsync(current.Address);
                pings.Remove(current.Address);
                ServerStatus.Text = "已删除 " + current.Address;
            };
            actions.Children.Add(ping);
            actions.Children.Add(edit);
            actions.Children.Add(delete);
            ServerListPanel.Children.Add(Row(copy, actions));
        }
    }

    static Control Row(Control copy, Control actions)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        row.Children.Add(copy);
        Grid.SetColumn(actions, 1);
        actions.Margin = new Thickness(12, 0, 0, 0);
        actions.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(actions);
        return new Border
        {
            BorderBrush = new SolidColorBrush(Color.Parse("#E3EAF0")),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(0, 6),
            Child = row
        };
    }

    static TextBlock Muted(string text) => new()
    {
        Text = text,
        Classes = { "muted" }
    };

    static void SetText(TextBox box, string text)
    {
        if (!box.IsFocused && box.Text != text)
            box.Text = text;
    }

    static void SetText(TextBlock block, string text) => block.Text = text;

    DataSourceSet ReadSources() => new(
        SourceAnnouncementsBox.Text?.Trim() ?? "",
        SourceServersBox.Text?.Trim() ?? "",
        SourceRecommendationsBox.Text?.Trim() ?? "",
        SourceQuotesBox.Text?.Trim() ?? "");

    static string Format(SystemInfoInfo info) =>
        string.Join(Environment.NewLine,
            "MCUG Launcher " + info.App,
            ".NET " + info.Dotnet,
            "WebView2 " + info.Webview,
            "系统 " + info.Os,
            "游戏目录 " + info.Root,
            "数据目录 " + info.Data);
}
