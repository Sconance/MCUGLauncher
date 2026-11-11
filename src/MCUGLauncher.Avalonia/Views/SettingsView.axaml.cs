using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using MCUGLauncher.Contracts;

namespace MCUGLauncher.Avalonia.Views;

internal partial class SettingsView : UserControl
{
    readonly Dictionary<string, Control> panels = new();
    readonly Dictionary<string, string> pings = new(StringComparer.OrdinalIgnoreCase);
    ILauncherService service = EmptyServiceFallback.Instance;
    LauncherState? state;
    string current = "launch";
    string editingServer = "";
    bool updatingRoots;
    bool addRootOnPick;

    public SettingsView()
    {
        InitializeComponent();
        // 合并后的常规分组：一个 key 对应一组卡片，组内多张卡一起显示
        panels["launch"] = LaunchGroup;
        panels["appearance"] = AppearancePanel;
        panels["favorites"] = FavoritesGroup;
        panels["community"] = CommunityGroup;
        panels["devtools"] = DevToolsGroup;
        panels["saves"] = SavesPanel;
        panels["about"] = AboutPanel;
        WireButtons();
        JavaListPanel.Children.Add(Muted("还没有扫描结果。点击“扫描本机 Java”。"));
        SaveListPanel.Children.Add(Muted("还没有存档备份。"));
        ServerListPanel.Children.Add(Muted("还没有收藏服务器。"));
        Nav.SelectedIndex = 0;
    }

    async Task SaveRoot(string path)
    {
        if (service is not null)
            LaunchStatus.Text = (await service.SaveSettingsAsync(new AppSettingsPatch(Root: path))).Message;
    }

    public SettingsView(ILauncherService service) : this()
    {
        Attach(service);
    }

    public void Attach(ILauncherService value)
    {
        if (service is not null)
            Detach();
        service = value ?? EmptyServiceFallback.Instance;
        service.StateChanged += ApplyState;
        service.PathPicked += OnPathPicked;
        service.ToastReceived += info => Status(info.Message);
        service.ErrorReceived += message => Status("操作失败：" + message);
        BuildPages(service);
        ApplyState(service.State);
    }

    /// <summary>内嵌的两个整页视图，与常规面板共用同一份服务实例。</summary>
    void BuildPages(ILauncherService value)
    {
        BookmarksHost.Content = new BookmarksView(value);
        CommunityHost.Content = new CommunityView(value);
    }

    void Detach()
    {
        if (service is null)
            return;
        service.StateChanged -= ApplyState;
        service.PathPicked -= OnPathPicked;
        service = EmptyServiceFallback.Instance;
        BookmarksHost.Content = null;
        CommunityHost.Content = null;
    }

    void WireButtons()
    {
        BrowseRootButton.Click += async (_, _) =>
        {
            addRootOnPick = false;
            await Pick(service?.PickFolderAsync("root"));
        };
        AddRootButton.Click += async (_, _) =>
        {
            addRootOnPick = true;
            await Pick(service?.PickFolderAsync("root"));
        };
        GameRootBox.SelectionChanged += async (_, _) =>
        {
            if (updatingRoots || service is null || GameRootBox.SelectedItem is not string root)
                return;
            if (root.Length == 0 || root == state?.Root)
                return;
            LaunchStatus.Text = "正在切换游戏目录…";
            var result = await service.SaveSettingsAsync(new AppSettingsPatch(Root: root));
            LaunchStatus.Text = result.Message;
        };
        BrowseJavaButton.Click += async (_, _) => await Pick(service?.PickFileAsync("java"));
        ScanJavaButton.Click += async (_, _) => await ScanJava(JavaStatus);
        SaveLaunchButton.Click += async (_, _) => await SaveLaunch();
        SaveJavaButton.Click += async (_, _) => await SaveJava();
        TestAnnouncementsButton.Click += async (_, _) => await TestSource("公告", SourceAnnouncementsBox);
        TestServersButton.Click += async (_, _) => await TestSource("服务器", SourceServersBox);
        TestRecommendationsButton.Click += async (_, _) => await TestSource("推荐", SourceRecommendationsBox);
        TestQuotesButton.Click += async (_, _) => await TestSource("语录", SourceQuotesBox);
        SaveSourcesButton.Click += async (_, _) => await SaveSources(use: false);
        UseCustomSourcesButton.Click += async (_, _) => await SaveSources(use: true);
        UseDefaultSourcesButton.Click += async (_, _) => await UseDefaultSources();
        ResetWindowButton.Click += async (_, _) => await service!.ResetWindowAsync();
        SaveServerButton.Click += async (_, _) => await SaveServer();
        CancelServerEditButton.Click += (_, _) => ResetServerForm();
        BackupSavesButton.Click += async (_, _) =>
        {
            SaveStatus.Text = (await service!.BackupSavesAsync()).Message;
            RenderSaveBackups(await service.ListSaveBackupsAsync());
        };
        OpenSavesFolderButton.Click += async (_, _) => await service!.OpenFolderAsync("saves");
        OpenConfigButton.Click += async (_, _) => await service!.OpenFolderAsync("config");
        ReadLogButton.Click += async (_, _) => await ReadLog();
        OpenLogFolderButton.Click += async (_, _) => await service!.OpenFolderAsync("config");
        ReadSystemInfoButton.Click += async (_, _) => await ReadSystemInfo();
        CopyDiagnosticsButton.Click += async (_, _) => await CopyDiagnostics();
        SaveAnonymousButton.Click += async (_, _) => await SaveAnonymous();
        VerifyCommunityKeyButton.Click += async (_, _) => await VerifyKey("community", CommunityKeyBox);
        SaveUiDebugButton.Click += async (_, _) => await SaveUiDebug();
        ChooseSkinButton.Click += async (_, _) => await service!.PickFileAsync("offlineSkin");
        ClearSkinButton.Click += async (_, _) =>
        {
            await service!.SetOfflineSkinAsync("");
            SkinStatus.Text = "已清除本地皮肤。";
        };
        ForgetMicrosoftButton.Click += async (_, _) => await ForgetAccount("microsoft");
        ForgetMuaButton.Click += async (_, _) => await ForgetAccount("mua");
    }

    async Task ForgetAccount(string mode)
    {
        if (service is null)
            return;
        AccountStatus.Text = "正在移除本机保存的账号…";
        AccountStatus.Text = (await service.ForgetAccountAsync(mode)).Message;
    }

    async Task SaveUiDebug()
    {
        if (service is null)
            return;
        UiDebugStatus.Text = "正在保存…";
        var result = await service.SaveSettingsAsync(new AppSettingsPatch(
            ForceTasksButton: ForceTasksButtonBox.IsChecked == true));
        UiDebugStatus.Text = result.Message;
    }

    static async Task Pick(Task? pending)
    {
        if (pending is not null)
            await pending;
    }

    async Task ScanJava(TextBlock status)
    {
        if (service is null)
            return;
        status.Text = "正在扫描本机 Java…";
        RenderJavaList(await service.ScanJavaAsync());
    }

    async Task SaveLaunch()
    {
        if (service is null)
            return;
        if (!int.TryParse(RamBox.Text, out var ram) || ram is < LaunchLimits.RamMin or > LaunchLimits.RamMax)
        {
            LaunchStatus.Text = $"最大内存需在 {LaunchLimits.RamMin}–{LaunchLimits.RamMax} MB 之间。";
            return;
        }

        if (!int.TryParse(WidthBox.Text, out var width) ||
            width is < LaunchLimits.WindowWidthMin or > LaunchLimits.WindowWidthMax)
        {
            LaunchStatus.Text = $"窗口宽需在 {LaunchLimits.WindowWidthMin}–{LaunchLimits.WindowWidthMax} 之间。";
            return;
        }

        if (!int.TryParse(HeightBox.Text, out var height) ||
            height is < LaunchLimits.WindowHeightMin or > LaunchLimits.WindowHeightMax)
        {
            LaunchStatus.Text = $"窗口高需在 {LaunchLimits.WindowHeightMin}–{LaunchLimits.WindowHeightMax} 之间。";
            return;
        }

        var root = RootBox.Text?.Trim() ?? "";
        if (root.Length == 0)
        {
            LaunchStatus.Text = "请选择已有游戏目录。";
            return;
        }

        LaunchStatus.Text = "正在保存…";
        var result = await service.SaveSettingsAsync(new AppSettingsPatch(
            Root: root,
            Java: JavaBox.Text?.Trim() ?? "",
            Ram: ram,
            Width: width,
            Height: height,
            AutoMemory: AutoMemoryBox.IsChecked == true,
            AutoChinese: AutoChineseBox.IsChecked == true));
        LaunchStatus.Text = result.Message;
    }

    async Task SaveJava()
    {
        if (service is null)
            return;
        JavaStatus.Text = "正在保存 Java 设置…";
        var result = await service.SaveSettingsAsync(new AppSettingsPatch(Java: JavaBox.Text?.Trim() ?? ""));
        JavaStatus.Text = result.Message;
    }

    async Task TestSource(string label, TextBox box)
    {
        if (service is null)
            return;
        var url = box.Text?.Trim() ?? "";
        if (url.Length == 0)
        {
            DataSourceStatus.Text = label + "地址为空。";
            return;
        }

        DataSourceStatus.Text = "正在测试" + label + "…";
        var result = await service.TestSourceAsync(label, url);
        OnTestResult(result);
    }

    async Task SaveSources(bool use)
    {
        if (service is null)
            return;
        await service.SaveCustomSourcesAsync(ReadSources());
        if (use)
        {
            await service.UseDataSourcesAsync("custom");
            DataSourceStatus.Text = "已保存并切换到自定义数据源。";
        }
        else
            DataSourceStatus.Text = "已保存自定义数据源，可点击“使用自定义”切换。";
    }

    async Task UseDefaultSources()
    {
        if (service is null)
            return;
        await service.UseDataSourcesAsync("default");
        DataSourceStatus.Text = "已切换到默认数据源。";
    }

    async Task SaveServer()
    {
        if (service is null)
            return;
        var address = ServerAddressBox.Text?.Trim() ?? "";
        if (address.Length == 0)
        {
            ServerStatus.Text = "请填写服务器地址。";
            return;
        }

        ServerStatus.Text = "正在保存…";
        var result = await service.SaveServerAsync(ServerNameBox.Text?.Trim() ?? "", address, editingServer);
        ServerStatus.Text = result.Message;
        if (result.Ok)
            ResetServerForm();
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
        if (service is null)
            return;
        var result = await service.SaveSettingsAsync(new AppSettingsPatch(Anonymous: AnonymousBox.IsChecked == true));
        CommunityStatus.Text = result.Message;
    }

    async Task VerifyKey(string role, TextBox box)
    {
        if (service is null)
            return;
        var key = box.Text?.Trim() ?? "";
        if (key.Length == 0)
        {
            CommunityStatus.Text = "请输入密钥。";
            return;
        }

        CommunityStatus.Text = "正在验证…";
        var result = await service.VerifyKeyAsync(role, key);
        CommunityStatus.Text = result.Message;
        if (result.Ok && role == "community")
            box.Text = "";
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

    /// <summary>供外壳跳转：key 是常规分组的面板名。</summary>
    public void Open(string key)
    {
        if (!panels.ContainsKey(key))
            return;
        foreach (var item in Nav.Items.OfType<ListBoxItem>())
            item.IsSelected = (string?)item.Tag == key;
        ShowPanel(key);
    }

    /// <summary>只显示对应的设置组，其余隐藏。</summary>
    void ShowPanel(string key)
    {
        current = key;
        foreach (var pair in panels)
            pair.Value.IsVisible = pair.Key == key;
        if (key == "saves")
            _ = LoadSaveBackups();
        else if (key == "devtools")
        {
            _ = ReadLog();
            _ = ReadSystemInfo();
        }
    }

    async Task ReadLog()
    {
        if (service is not null)
            LogBox.Text = await service.ReadLogAsync();
    }

    async Task LoadSaveBackups()
    {
        if (service is not null)
            RenderSaveBackups(await service.ListSaveBackupsAsync());
    }

    async Task ReadSystemInfo()
    {
        if (service is not null)
            DiagnosticsBox.Text = Format(await service.SystemInfoAsync());
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
                _ = SaveRoot(info.Path);
            }
            else
                LaunchStatus.Text = "已选择游戏目录，保存后生效。";
        }
        else if (info.Target == "java")
        {
            JavaBox.Text = info.Path;
            JavaStatus.Text = "已选择 Java 路径，保存后生效。";
        }
        else if (info.Target == "offlineSkin")
        {
            _ = ApplyOfflineSkin(info.Path);
        }
    }

    async Task ApplyOfflineSkin(string path)
    {
        if (service is null)
            return;
        await service.SetOfflineSkinAsync(path);
        SkinStatus.Text = "已应用本地皮肤。";
    }

    async Task PingServer(string address)
    {
        var info = await service!.PingServerAsync(address, address);
        pings[info.Id] = info.Online
            ? $"{info.Players}/{info.Max} 人 · {info.Ping} ms · {info.Motd}"
            : "离线 · " + info.Error;
        RenderServers();
    }

    void OnTestResult(TestResultInfo info)
    {
        DataSourceStatus.Text = $"{info.Name}：{(info.Ok ? "可用" : "失败")} · {info.Message} · {info.Ms} ms";
    }

    void Status(string message)
    {
        switch (current)
        {
            case "launch":
                LaunchStatus.Text = message;
                break;
            case "favorites":
                ServerStatus.Text = message;
                break;
            case "community":
                CommunityStatus.Text = message;
                break;
            case "devtools":
                DiagnoseStatus.Text = message;
                break;
            case "saves":
                SaveStatus.Text = message;
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
        SetText(RamBox, snapshot.Ram.ToString());
        SetText(WidthBox, snapshot.Width.ToString());
        SetText(HeightBox, snapshot.Height.ToString());
        SetText(AboutVersion, "MCUG Launcher " + snapshot.AppVersion);
        if (!AutoMemoryBox.IsFocused)
            AutoMemoryBox.IsChecked = snapshot.AutoMemory;
        if (!AutoChineseBox.IsFocused)
            AutoChineseBox.IsChecked = snapshot.AutoChinese;
        if (!AnonymousBox.IsFocused)
            AnonymousBox.IsChecked = snapshot.Anonymous;
        if (!ForceTasksButtonBox.IsFocused)
            ForceTasksButtonBox.IsChecked = snapshot.ForceTasksButton;
        var sources = snapshot.CustomDataSources.Announcements.Length > 0
            ? snapshot.CustomDataSources
            : snapshot.DataSources;
        SetText(SourceAnnouncementsBox, sources.Announcements);
        SetText(SourceServersBox, sources.Servers);
        SetText(SourceRecommendationsBox, sources.Recommendations);
        SetText(SourceQuotesBox, sources.Quotes);
        var mode = snapshot.DataSourceMode == "custom" ? "自定义" : "默认";
        var keys =
            $"身份密钥：{(snapshot.HasCommunityKey ? "已设置" : "未设置")} · 管理员密钥：{(snapshot.HasAdminKey ? "已设置" : "未设置")}";
        // 「社区」分组同时包含数据源与身份两块，两个状态一起刷新
        if (current == "community")
        {
            DataSourceStatus.Text = $"当前使用{mode}数据源。";
            CommunityStatus.Text = keys;
        }

        RenderServers();
        if (service is { JavaList.Count: > 0 })
            RenderJavaList(service.JavaList);
        if (service is { SaveBackups.Count: > 0 })
            RenderSaveBackups(service.SaveBackups);
        var offlineAccount = snapshot.AccountType == "offline";
        ChooseSkinButton.IsEnabled = offlineAccount;
        ClearSkinButton.IsEnabled = offlineAccount;
        SkinStatus.Text = offlineAccount ? "" : "本地皮肤只对离线账号生效。";
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
            copy.Children.Add(new TextBlock
                { Text = item.Path, Classes = { "muted" }, TextWrapping = TextWrapping.Wrap });
            if (item.Error.Length > 0)
                copy.Children.Add(new TextBlock
                    { Text = item.Error, Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap });
            var use = new Button
                { Content = "使用", Classes = { "ghost" }, VerticalAlignment = VerticalAlignment.Center };
            var path = item.Path;
            use.Click += (_, _) =>
            {
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
                var restore = new Button
                    { Content = "恢复为 " + world, Classes = { "ghost" }, HorizontalAlignment = HorizontalAlignment.Left };
                var id = backup.Id;
                var name = world;
                restore.Click += async (_, _) =>
                {
                    SaveStatus.Text = "正在恢复存档…";
                    SaveStatus.Text = (await service!.RestoreSaveAsync(id, name)).Message;
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
                await PingServer(current.Address);
            };
            var edit = new Button { Content = "编辑", Classes = { "ghost" } };
            edit.Click += (_, _) => BeginEdit(current);
            var delete = new Button { Content = "删除", Classes = { "ghost" } };
            delete.Click += async (_, _) =>
            {
                var result = await service!.DeleteServerAsync(current.Address);
                pings.Remove(current.Address);
                ServerStatus.Text = result.Message;
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
