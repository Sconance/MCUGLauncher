using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using MCUGLauncher.Contracts;
using SSvg = Avalonia.Svg.Skia.Svg;

namespace MCUGLauncher.Avalonia.Views;

internal partial class HomeView : UserControl
{
    readonly Dictionary<string, ServerPingInfo> pings = new(StringComparer.OrdinalIgnoreCase);
    ILauncherService service = EmptyServiceFallback.Instance;
    LauncherState? state;
    CommunityData community = CommunityData.Empty;
    Bitmap? announcementImage;
    Bitmap? avatar;
    int imageVersion;
    string serverSignature = "";
    string editingServer = "";
    string dialogInstance = "";
    bool dialogShowInstances;
    int quoteIndex;

    string avatarAccount = "";

    // 当前正在查看的账号页签，可以停在「正版」但还没登录
    string accountTab = "";

    // 鼠标是否停在左栏上；刷新按钮只在这个状态下出现
    bool accountHover;

    public HomeView()
    {
        InitializeComponent();
        WireButtons();
    }

    public HomeView(ILauncherService service) : this()
    {
        Attach(service);
    }

    public void Attach(ILauncherService value)
    {
        service = value ?? EmptyServiceFallback.Instance;
        service.StateChanged += ApplyState;
        service.CommunityChanged += OnCommunity;
        service.ToastReceived += info => Status(info.Message);
        service.ErrorReceived += message => Status("操作失败：" + message);
        service.PathPicked += OnPathPicked;
        service.MuaProfilesReceived += OnMuaProfiles;
        // 顶部快捷按钮现在只有下载任务，「运行中的游戏」改为这里的浮窗；实例只建一次
        GamesHost.Content ??= new GamesView(service);
        community = service.Community;
        ApplyState(service.State);
        RenderCommunity();
    }

    /// <summary>账号恢复完成后由外壳调用，避免在 ready 之前读取头像。</summary>
    public void Activate() => _ = ReadAvatar();

    /// <summary>切换账号页签：先换视图，只有该模式已有凭据时才真正切换启动账号。</summary>
    void ShowAccountTab(string mode)
    {
        accountTab = mode;
        RenderAccount();
        if (state is null || state.AccountType == mode)
            return;
        var hasIdentity = mode switch
        {
            "microsoft" => state.HasMicrosoftAccount,
            "mua" => state.HasMuaAccount,
            _ => true
        };
        if (hasIdentity)
            _ = service!.SwitchAccountModeAsync(mode);
    }

    /// <summary>三按钮 + 差异化面板：没有凭据的模式只显示登录入口。</summary>
    void RenderAccount()
    {
        var tab = accountTab.Length > 0 ? accountTab : "offline";
        var microsoft = state?.HasMicrosoftAccount == true;
        var mua = state?.HasMuaAccount == true;
        var player = state?.Player ?? "";

        MicrosoftModeButton.Classes.Set("active", tab == "microsoft");
        OfflineModeButton.Classes.Set("active", tab == "offline");
        MuaModeButton.Classes.Set("active", tab == "mua");

        MicrosoftPanel.IsVisible = tab == "microsoft";
        OfflinePanel.IsVisible = tab == "offline";
        MuaPanel.IsVisible = tab == "mua";

        // 没登录过的模式不显示头像，避免出现一张空图
        AvatarBox.IsVisible = tab switch
        {
            "microsoft" => microsoft,
            "mua" => mua,
            _ => true
        };

        MicrosoftName.Text = player;
        MicrosoftMeta.Text = AccountText.Label("microsoft");
        MicrosoftName.IsVisible = microsoft;
        MicrosoftMeta.IsVisible = microsoft;
        MicrosoftHint.IsVisible = !microsoft;
        MicrosoftHint.Text = "还没有登录过正版账号。";
        MicrosoftLoginButton.IsVisible = !microsoft;

        if (!OfflineNameBox.IsFocused)
            OfflineNameBox.Text = player;

        MuaName.Text = player;
        MuaName.IsVisible = mua;
        MuaAccountBox.IsVisible = !mua;
        MuaPasswordBox.IsVisible = !mua;
        MuaLoginButton.IsVisible = !mua;
        if (mua)
            MuaHint.Text = AccountText.Label("mua");
        else if (string.IsNullOrEmpty(MuaHint.Text))
            MuaHint.Text = "还没有登录过 MUA 账号。";

        UpdateRefreshVisibility();
    }

    void OnAccountPanelPointerEntered(object? sender, PointerEventArgs e)
    {
        accountHover = true;
        UpdateRefreshVisibility();
    }

    void OnAccountPanelPointerExited(object? sender, PointerEventArgs e)
    {
        accountHover = false;
        UpdateRefreshVisibility();
    }

    /// <summary>
    /// 悬停在账号栏、且当前页签真有皮肤可读时点亮按钮。
    /// 只改 Opacity 不改 IsVisible：控件始终占位，淡入淡出才有动画可插值，
    /// 也让下方「当前实例」那一行不会因为按钮显隐而跳动。
    /// </summary>
    void UpdateRefreshVisibility() =>
        RefreshAccountButton.Opacity = accountHover && AvatarBox.IsVisible ? 1 : 0;

    void WireButtons()
    {
        MicrosoftModeButton.Click += (_, _) => ShowAccountTab("microsoft");
        OfflineModeButton.Click += (_, _) => ShowAccountTab("offline");
        MuaModeButton.Click += (_, _) => ShowAccountTab("mua");
        MicrosoftLoginButton.Click += async (_, _) =>
        {
            MicrosoftHint.Text = "正在打开微软登录页…";
            MicrosoftHint.IsVisible = true;
            var result = await service!.LoginMicrosoftAsync();
            MicrosoftHint.Text = result.Message;
            await service.RequestStateAsync();
        };
        MuaLoginButton.Click += async (_, _) =>
        {
            var account = MuaAccountBox.Text?.Trim() ?? "";
            var password = MuaPasswordBox.Text ?? "";
            if (account.Length == 0 || password.Length == 0)
            {
                MuaHint.Text = "请填写 MUA 账号和密码。";
                return;
            }

            MuaHint.Text = "正在登录 MUA…";
            var result = await service!.LoginMuaAsync(account, password);
            MuaHint.Text = result.Message;
            await service.RequestStateAsync();
        };
        // 刷新：重新拉一次账号状态，再重读皮肤。头像缓存按「模式 + 玩家名」去重，
        // 只在状态真正变化时才会自动重读，所以这里必须显式再读一遍。
        RefreshAccountButton.Click += async (_, _) =>
        {
            RefreshAccountButton.IsEnabled = false;
            try
            {
                await service!.RequestStateAsync();
                await ReadAvatar();
            }
            finally
            {
                RefreshAccountButton.IsEnabled = true;
            }
        };
        // 玩家名不用保存按钮：输入即静默写入配置，启动时直接读
        OfflineNameBox.TextChanged += async (_, _) =>
        {
            if (accountTab != "offline")
                return;
            var name = OfflineNameBox.Text?.Trim() ?? "";
            if (name.Length == 0 || name == state?.Player)
                return;
            await service!.SaveSettingsAsync(new AppSettingsPatch(Player: name));
        };
        LaunchButton.Click += async (_, _) =>
        {
            var target = state?.SelectedInstance is { Length: > 0 } instance ? instance : state?.SelectedVersion ?? "";
            if (target.Length == 0)
            {
                Status("请先安装或选择实例。");
                return;
            }

            Status("正在准备启动…");
            Status((await service!.LaunchAsync(target)).Message);
        };
        InstanceButton.Click += (_, _) =>
        {
            // 实例设置里点「当前实例」= 退出设置、直接回到实例选择
            if (InstanceSettingsHost.IsVisible)
            {
                BackToInstancePicker();
                return;
            }

            ToggleInstances(!InstancePicker.IsVisible);
        };
        VersionSettingsButton.Click += (_, _) =>
        {
            // 已在实例设置里，再点一次当作退出，回到信息流
            if (InstanceSettingsHost.IsVisible)
            {
                ExitInstanceSettings();
                return;
            }

            ShowInstanceSettings(state?.SelectedInstance ?? "");
        };
        RunningGameButton.Click += (_, _) => (GamesHost.Content as GamesView)?.Activate();
        InstancePicker.CloseRequested += () => ToggleInstances(false);
        InstancePicker.OpenRootRequested += async () => await service!.OpenFolderAsync("root");
        InstancePicker.RefreshRequested += async () => await service!.RequestStateAsync();
        InstancePicker.InstancePicked += async name =>
        {
            await service!.SelectVersionAsync(name);
            await service!.SelectInstanceAsync(name);
            // 留在二级页面：选中项会高亮，方便连续比较几个实例
        };
        InstancePicker.LaunchRequested += async name =>
        {
            InstancePicker.SetStatus("正在用 " + name + " 准备启动…");
            InstancePicker.SetStatus((await service!.LaunchAsync(name)).Message);
        };
        // 只打开这一级的设置，不切换当前实例；模组列表跟随全局，需要时在设置页里手动切
        InstancePicker.SettingsRequested += name => ShowInstanceSettings(name);
        AddServerButton.Click += (_, _) => ShowServerDialog(null);
        AddRootButton.Click += async (_, _) => await service!.PickFolderAsync("root");
        DialogInstanceButton.Click += (_, _) => RenderDialogPickers();
        DialogCancelButton.Click += (_, _) => HideServerDialog();
        DialogSaveButton.Click += async (_, _) => await SaveServerFromDialog();
        DialogServerNameBox.KeyDown += OnDialogTextBoxKeyDown;
        DialogServerAddressBox.KeyDown += OnDialogTextBoxKeyDown;
        ServerDialogLayer.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape)
                return;
            e.Handled = true;
            HideServerDialog();
        };
        OpenAnnouncementButton.Click += async (_, _) => await OpenAnnouncement();
        NextQuoteButton.Click += (_, _) =>
        {
            if (community.Quotes.Count == 0)
                return;
            quoteIndex = (quoteIndex + 1) % community.Quotes.Count;
            RenderQuote();
        };
        RefreshCommunityButton.Click += async (_, _) =>
        {
            HomeStatus.Text = "正在刷新社区内容…";
            await service!.RefreshCommunityAsync();
        };
    }

    void ApplyState(LauncherState? snapshot)
    {
        state = snapshot;
        if (snapshot is null)
            return;
        if (accountTab.Length == 0)
            accountTab = snapshot.AccountType;
        RenderAccount();
        var avatarKey = snapshot.AccountType + "|" + snapshot.Player + "|"
                        + snapshot.HasMicrosoftAccount + snapshot.HasMuaAccount;
        if (avatarKey != avatarAccount)
        {
            avatarAccount = avatarKey;
            _ = ReadAvatar();
        }

        var current = snapshot.Instances.FirstOrDefault(item => item.Name == snapshot.SelectedInstance)
                      ?? snapshot.Instances.FirstOrDefault(item => item.Name == snapshot.SelectedVersion);
        LaunchButton.IsEnabled = current is not null;
        ShowInstanceButton(current);
        InstancePicker.SetState(snapshot, current?.Name ?? snapshot.SelectedInstance);
        var signature = string.Join("|", snapshot.Servers.Select(item => item.Address));
        if (signature != serverSignature)
        {
            serverSignature = signature;
            pings.Clear();
            foreach (var server in snapshot.Servers)
                if (server.Address.Length > 0)
                    _ = PingServer(server.Address);
        }

        RenderServers();
        // 目录切换、实例增删都会重新推状态，两个列表跟着重建
        if (RootPanel.IsVisible)
            RenderRoots();
        if (ServerDialogLayer.IsVisible)
            RenderDialogPickers();
    }

    /// <summary>左侧「当前实例」按钮：图标、名称与「版本类型 版本号，加载器」一行说明。</summary>
    void ShowInstanceButton(InstanceInfo? current)
    {
        if (current is null)
        {
            InstanceButtonIcon.Background = new SolidColorBrush(Color.Parse("#C9D2DA"));
            InstanceButtonIcon.ClipToBounds = false;
            InstanceButtonGlyph.IsVisible = true;
            InstanceButtonGlyph.Text = "·";
            InstanceButtonImage.IsVisible = false;
            InstanceButtonImage.Source = null;
            InstanceButtonTitle.Text = "未选择实例";
            InstanceButtonDetail.Text = "点这里选择要启动的实例";
            return;
        }

        var row = InstanceListItem.From(current);
        var icon = row.Icon;
        // 有加载器图片就铺满图标框（圆角裁剪），否则退回彩色徽标 + 字符
        InstanceButtonIcon.Background = icon is null ? row.Accent : Brushes.Transparent;
        InstanceButtonIcon.ClipToBounds = icon is not null;
        InstanceButtonGlyph.IsVisible = icon is null;
        InstanceButtonGlyph.Text = row.Glyph;
        InstanceButtonImage.Source = icon;
        InstanceButtonImage.IsVisible = icon is not null;
        InstanceButtonTitle.Text = row.Title;
        InstanceButtonDetail.Text = row.Subtitle;
    }

    /// <summary>切换右侧信息流与实例选择二级页面；左侧账号栏同时换成实例目录栏，按钮右箭头翻转。</summary>
    void ToggleInstances(bool open)
    {
        InstancePicker.IsVisible = open;
        FeedScroll.IsVisible = !open;
        AccountPanel.IsVisible = !open;
        RootPanel.IsVisible = open;
        InstanceButtonChevron.Text = open ? "‹" : "›";
        if (open)
            RenderRoots();
        else
            InstancePicker.SetStatus("");
    }

    /// <summary>打开实例设置：与「选择实例」同级的二级界面，但左栏保持账号栏。</summary>
    void ShowInstanceSettings(string name)
    {
        if (InstanceSettingsHost.Content is not InstancesView view)
        {
            view = new InstancesView(service!);
            // 设置页里的「收起」直接退回一级菜单（首页信息流），不经过实例选择
            view.CloseRequested += ExitInstanceSettings;
            InstanceSettingsHost.Content = view;
        }

        FeedScroll.IsVisible = false;
        InstancePicker.IsVisible = false;
        InstanceSettingsHost.IsVisible = true;
        // 设置页里要能直接点「当前实例」或「版本设置」，所以左栏回到账号栏
        AccountPanel.IsVisible = true;
        RootPanel.IsVisible = false;
        InstanceButtonChevron.Text = "‹";
        view.Open(name ?? "");
    }

    /// <summary>「收起」与再点一次齿轮：退回一级菜单。</summary>
    void ExitInstanceSettings()
    {
        InstanceSettingsHost.IsVisible = false;
        ToggleInstances(false);
    }

    /// <summary>点「当前实例」：退回实例选择。</summary>
    void BackToInstancePicker()
    {
        InstanceSettingsHost.IsVisible = false;
        ToggleInstances(true);
    }

    /// <summary>左侧目录栏：列出所有游戏目录，当前目录高亮，点另一个即切换。</summary>
    void RenderRoots()
    {
        RootList.Children.Clear();
        if (state is null)
        {
            RootSummary.Text = "";
            return;
        }

        var roots = state.GameRoots;
        RootSummary.Text = roots.Count == 0
            ? "还没有添加目录"
            : "共 " + roots.Count + " 个目录 · 点选切换";
        foreach (var root in roots)
            RootList.Children.Add(RootRow(root, root.Equals(state.Root, StringComparison.OrdinalIgnoreCase)));
    }

    Control RootRow(string path, bool active)
    {
        var folder = new SSvg(new Uri("avares://MCUGLauncher/"))
        {
            Path = "/Assets/Icons/FolderIcon.svg",
            Height = 15,
            Width = 15,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        SSvg.SetCss(folder, "path { stroke: white; }");
        var icon = new Border
        {
            Width = 26,
            Height = 26,
            CornerRadius = new CornerRadius(7),
            VerticalAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(Color.Parse(active ? "#3370AA" : "#C9D2DA")),
            // 原来这里是 Segoe MDL2 的 \uE8B7 字形；换成统一的文件夹图标资源，
            // 描边原是 #333，压在蓝/灰底章上要改成白色才看得清
            Child = folder
        };

        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (name.Length == 0)
            name = path;
        var lines = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        lines.Children.Add(new TextBlock
        {
            Text = name,
            FontSize = 12.5,
            FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        lines.Children.Add(new TextBlock
        {
            Text = path,
            FontSize = 10.5,
            Classes = { "muted" },
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 9 };
        grid.Children.Add(icon);
        Grid.SetColumn(lines, 1);
        grid.Children.Add(lines);

        var row = new Button
        {
            Content = grid,
            Classes = { "instanceRow" },
            Padding = new Thickness(6, 4),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        if (active)
            row.Classes.Set("selected", true);
        row.Click += async (_, _) =>
        {
            if (active)
                return;
            Status("正在切换到 " + name + "…");
            var result = await service!.SaveSettingsAsync(new AppSettingsPatch(Root: path));
            Status(result.Message);
        };
        return row;
    }

    void OnPathPicked(PickedPathInfo info)
    {
        if (info.Target != "root" || info.Path.Length == 0)
            return;
        _ = AddRootAsync(info.Path);
    }

    async Task AddRootAsync(string path)
    {
        Status("正在添加实例目录…");
        var result = await service!.SaveSettingsAsync(new AppSettingsPatch(Root: path));
        Status(result.Message);
    }

    IReadOnlyList<InstanceInfo> Instances() => state is null
        ? Array.Empty<InstanceInfo>()
        : state.Instances
            .Where(item => item.Name.Length > 0 && !item.IsPlaceholder)
            .ToArray();

    void RenderServers()
    {
        ServerPanel.Children.Clear();
        if (state is not { Servers.Count: > 0 })
        {
            ServerPanel.IsVisible = false;
            ServerEmptyPanel.IsVisible = true;
            ServerSummary.Text = "0 台服务器";
            return;
        }

        ServerPanel.IsVisible = true;
        ServerEmptyPanel.IsVisible = false;
        var online = state.Servers.Count(server => pings.TryGetValue(server.Address, out var ping) && ping.Online);
        ServerSummary.Text = $"{online} 在线 · {state.Servers.Count - online} 离线";
        foreach (var server in state.Servers)
        {
            pings.TryGetValue(server.Address, out var ping);
            var icon = new Border
            {
                Width = 34,
                Height = 34,
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.Parse(ping?.Online == true ? "#3370AA" : "#8A96A0")),
                Child = new SSvg(new Uri("avares://MCUGLauncher/"))
                {
                    Path = "/Assets/Icons/ServerIcon.svg",
                    Height = 16, Width = 16,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            var heading = new StackPanel { Spacing = 2 };
            heading.Children.Add(new TextBlock
            {
                Text = server.Name.Length > 0 ? server.Name : server.Address,
                FontWeight = FontWeight.SemiBold,
                FontSize = 12.5
            });
            heading.Children.Add(new TextBlock
            {
                Text = ping is null ? "尚未查询" : ping.Online ? $"{ping.Players}/{ping.Max} 人 · {ping.Ping} ms" : "离线",
                Classes = { "muted" }
            });
            var top = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            top.Children.Add(icon);
            Grid.SetColumn(heading, 1);
            heading.Margin = new Thickness(9, 0, 0, 0);
            top.Children.Add(heading);
            var copy = new StackPanel { Spacing = 5 };
            copy.Children.Add(top);
            copy.Children.Add(new TextBlock
            {
                Text = server.Address,
                Classes = { "muted" },
                TextWrapping = TextWrapping.Wrap
            });
            copy.Children.Add(new TextBlock
            {
                Text = server.Instance.Length > 0 ? "实例 · " + server.Instance : "未绑定实例",
                Classes = { "muted" },
                TextWrapping = TextWrapping.Wrap
            });
            copy.Children.Add(new TextBlock
            {
                Text = ping is null ? "等待查询" : ping.Online ? ping.Motd : "无法连接",
                TextWrapping = TextWrapping.Wrap,
                Classes = { "muted" }
            });
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            var current = server;
            var open = new Button { Content = "打开", Classes = { "link" } };
            open.Click += async (_, _) => await OpenServerAsync(current);
            var query = new Button { Content = "查询", Classes = { "link" } };
            query.Click += async (_, _) => await PingServer(current.Address);
            var edit = new Button { Content = "编辑", Classes = { "link" } };
            edit.Click += (_, _) => ShowServerDialog(current);
            var delete = new Button { Content = "删除", Classes = { "link" } };
            delete.Click += async (_, _) =>
            {
                var result = await service!.DeleteServerAsync(current.Address);
                pings.Remove(current.Address);
                Status(result.Message);
            };
            actions.Children.Add(open);
            actions.Children.Add(query);
            actions.Children.Add(edit);
            actions.Children.Add(delete);
            copy.Children.Add(actions);
            ServerPanel.Children.Add(new Border
            {
                Width = 200,
                MinHeight = 154,
                Margin = new Thickness(0, 0, 10, 10),
                Padding = new Thickness(11),
                CornerRadius = new CornerRadius(9),
                Background = new SolidColorBrush(Color.Parse("#FDFEFD")),
                BorderBrush = new SolidColorBrush(Color.Parse("#DDE4EB")),
                BorderThickness = new Thickness(1),
                Child = copy
            });
        }
    }

    void OnCommunity(CommunityData data)
    {
        community = data;
        quoteIndex = Math.Clamp(quoteIndex, 0, Math.Max(0, data.Quotes.Count - 1));
        RenderCommunity();
        HomeStatus.Text = "社区内容已刷新。";
    }

    void RenderCommunity()
    {
        RenderAnnouncement();
        RenderRecommendations();
        RenderQuote();
    }

    void RenderAnnouncement()
    {
        OpenAnnouncementButton.IsVisible = false;
        if (community.Announcements.Count == 0)
        {
            AnnouncementTitle.Text = "暂无公告";
            AnnouncementBody.Text = "新的群公告会出现在这里。";
            AnnouncementMeta.Text = "";
            _ = ShowAnnouncementImage("");
            return;
        }

        var item = community.Announcements[0];
        AnnouncementTitle.Text = item.Tag.Length > 0
            ? "[" + item.Tag + "] " + item.Title
            : item.Title;
        AnnouncementBody.Text = item.Body;
        AnnouncementMeta.Text = string.Join(" · ", new[]
        {
            item.Author,
            item.PublishedAt?.ToString("yyyy-MM-dd") ?? "",
            item.ImageUrl.Length > 0 ? "含配图" : ""
        }.Where(value => value.Length > 0));
        OpenAnnouncementButton.IsVisible = item.Markdown.Length > 0;
        _ = ShowAnnouncementImage(item.ImageUrl);
    }

    async Task OpenAnnouncement()
    {
        if (community.Announcements.Count == 0 || community.Announcements[0].Markdown.Length == 0)
            return;
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            Status("无法打开公告窗口。");
            return;
        }

        await new AnnouncementWindow(community.Announcements[0]).ShowDialog(owner);
    }

    async Task ShowAnnouncementImage(string url)
    {
        var version = ++imageVersion;
        announcementImage?.Dispose();
        announcementImage = null;
        AnnouncementImage.Source = null;
        AnnouncementImage.IsVisible = false;
        if (url.Length == 0)
            return;
        try
        {
            byte[] bytes;
            if (url.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            {
                var comma = url.IndexOf(',');
                if (comma < 0)
                    return;
                bytes = Convert.FromBase64String(url[(comma + 1)..]);
            }
            else if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                     || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                if (service is null)
                    return;
                bytes = await service.DownloadImageAsync(url);
            }
            else
                return;

            if (version != imageVersion)
                return;
            using var input = new MemoryStream(bytes);
            announcementImage = new Bitmap(input);
            AnnouncementImage.Source = announcementImage;
            AnnouncementImage.IsVisible = true;
        }
        catch
        {
            if (version != imageVersion)
                return;
            announcementImage = null;
            AnnouncementImage.Source = null;
            AnnouncementImage.IsVisible = false;
        }
    }

    void RenderRecommendations()
    {
        RecommendationPanel.Children.Clear();
        if (community.Recommendations.Count == 0)
        {
            RecommendationPanel.Children.Add(Muted("暂无群友推荐。"));
            return;
        }

        foreach (var item in community.Recommendations.Take(5))
        {
            var isServer = item.Type is not ("material" or "shader");
            var type = item.Type switch
            {
                "material" => "材质包",
                "shader" => "光影",
                _ => "服务器"
            };
            var copy = new StackPanel { Spacing = 2 };
            copy.Children.Add(new TextBlock { Text = item.Name, FontWeight = FontWeight.SemiBold });
            copy.Children.Add(new TextBlock
            {
                Text = $"{type} · 由 {item.Author} 推荐" +
                       (item.Tags.Count > 0 ? " · " + string.Join(" / ", item.Tags) : ""),
                Classes = { "muted" }
            });
            if (item.Description.Length > 0)
                copy.Children.Add(new TextBlock
                {
                    Text = item.Description,
                    TextWrapping = TextWrapping.Wrap,
                    Classes = { "muted" }
                });
            // Svg 控件没有无参构造函数：XAML 里 <Svg/> 由加载器传 baseUri，代码里必须自己给一个，
            // 否则 "/Assets/x.svg" 这种以 / 开头的路径无法解析成 avares 资源。
            var iconFile = item.Type switch
            {
                "material" => "ResourcePackIcon",
                "shader" => "ResourcePackIcon",
                _ => "ServerIcon"
            };
            var icon = new Border
            {
                Width = 30,
                Height = 30,
                CornerRadius = new CornerRadius(7),
                Background = new SolidColorBrush(Color.Parse(item.Type switch
                {
                    "material" => "#C9822B",
                    "shader" => "#7B5AC8",
                    _ => "#3370AA"
                })),
                Child = new SSvg(new Uri("avares://MCUGLauncher/"))
                {
                    Path = "/Assets/Icons/" + iconFile + ".svg",
                    Height = 16, Width = 16,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            row.Children.Add(icon);
            Grid.SetColumn(copy, 1);
            copy.Margin = new Thickness(9, 0, 0, 0);
            row.Children.Add(copy);
            var card = new Border
            {
                // 透明背景只为可命中：Border 没有背景时空白处收不到右键
                Background = Brushes.Transparent,
                BorderBrush = new SolidColorBrush(Color.Parse("#E7EEF4")),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(0, 6),
                Child = row
            };
            // 只有服务器类推荐才能收藏，材质包/光影不挂右键菜单
            if (isServer)
                WireFavoriteMenu(card, item);
            RecommendationPanel.Children.Add(card);
        }
    }

    /// <summary>
    /// 给整行挂右键菜单。没用 ContextFlyout：Control 只在按下点正好落在控件本身时才自动弹出
    /// （OnPointerReleased 里要求 e.Source == this），点在文字或图标上就没有反应。
    /// </summary>
    void WireFavoriteMenu(Border card, RecommendationInfo item)
    {
        var favorite = new MenuItem { Header = "收藏" };
        favorite.Click += async (_, _) => await FavoriteServerAsync(item);
        var flyout = new MenuFlyout();
        flyout.Items.Add(favorite);
        card.PointerReleased += (_, e) =>
        {
            if (e.InitialPressMouseButton != MouseButton.Right)
                return;
            flyout.ShowAt(card, true);
            e.Handled = true;
        };
    }

    /// <summary>走「添加服务器」同一套后台逻辑：SaveServerAsync(名称, 地址, 空旧地址) 即新增收藏。</summary>
    async Task FavoriteServerAsync(RecommendationInfo item)
    {
        var address = item.Url.Trim();
        if (address.Length == 0)
        {
            Status("这条推荐没有附带服务器地址，无法收藏。");
            return;
        }

        if (state is not null &&
            state.Servers.Any(server => server.Address.Equals(address, StringComparison.OrdinalIgnoreCase)))
        {
            Status("「" + (item.Name.Length > 0 ? item.Name : address) + "」已经在收藏里了。");
            return;
        }

        Status("正在收藏 " + address + "…");
        var result = await service!.SaveServerAsync(item.Name.Trim(), address, "");
        Status(result.Message);
    }

    void RenderQuote()
    {
        NextQuoteButton.IsEnabled = community.Quotes.Count > 0;
        if (community.Quotes.Count == 0)
        {
            QuoteText.Text = "暂无语录。";
            QuoteMeta.Text = "";
            return;
        }

        var quote = community.Quotes[quoteIndex];
        QuoteText.Text = quote.Type == "image" ? "[图片语录] " + quote.Text : quote.Text;
        QuoteMeta.Text = string.Join(" · ", new[] { quote.Author, quote.Source }.Where(value => value.Length > 0));
    }

    /// <summary>MUA 账号下有多个角色时，在这里直接选。</summary>
    void OnMuaProfiles(MuaProfilesInfo info)
    {
        MuaProfilesPanel.Children.Clear();
        if (info.Items.Count == 0)
            return;
        MuaHint.Text = "这个账号有多个角色，请选择：";
        foreach (var profile in info.Items)
        {
            var button = new Button
            {
                Content = profile.Name,
                Classes = { "ghost" },
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            var token = info.Token;
            var id = profile.Id;
            button.Click += async (_, _) =>
            {
                await service!.SelectMuaProfileAsync(token, id);
                MuaProfilesPanel.Children.Clear();
                await service.RequestStateAsync();
            };
            MuaProfilesPanel.Children.Add(button);
        }
    }

    void OnAvatar(AvatarInfo info)
    {
        avatar?.Dispose();
        avatar = Decode(info.Image);
        AvatarImage.Source = avatar;
    }

    async Task ReadAvatar()
    {
        if (service is null)
            return;
        var info = await service.ReadAvatarAsync();
        if (info is not null)
            OnAvatar(info);
    }

    static Bitmap? Decode(string dataUrl)
    {
        var comma = dataUrl.IndexOf(',');
        if (comma < 0 || comma + 1 >= dataUrl.Length)
            return null;
        try
        {
            var bytes = Convert.FromBase64String(dataUrl[(comma + 1)..]);
            using var input = new MemoryStream(bytes);
            return new Bitmap(input);
        }
        catch
        {
            return null;
        }
    }

    async Task PingServer(string address)
    {
        var info = await service!.PingServerAsync(address, address);
        pings[info.Id] = info;
        RenderServers();
    }

    /// <summary>用收藏绑定的实例直连服务器；未绑定则先引导去绑定。</summary>
    async Task OpenServerAsync(SavedServerInfo server)
    {
        if (server.Instance.Length == 0)
        {
            ShowServerDialog(server);
            SetDialogStatus("先给这台服务器绑定实例，保存后就能一键进服。");
            return;
        }

        Status("正在用 " + server.Instance + " 打开 " + server.Address + "…");
        Status((await service!.LaunchToServerAsync(server.Instance, server.Address)).Message);
    }

    /// <summary>打开中间覆盖页；server 为空表示新增。</summary>
    void ShowServerDialog(SavedServerInfo? server)
    {
        editingServer = server?.Address ?? "";
        ServerDialogTitle.Text = server is null ? "添加服务器" : "编辑服务器";
        DialogServerNameBox.Text = server?.Name ?? "";
        DialogServerAddressBox.Text = server?.Address ?? "";
        SetDialogStatus("");
        FillDialogInstances(server?.Instance ?? "");
        DialogSaveButton.IsEnabled = true;
        ServerDialogLayer.IsVisible = true;
        // 卡片自身 Cycle 收住 Tab，页面只做模糊与遮罩，不改背景控件的可交互状态
        PageRoot.Effect = new BlurEffect { Radius = 7 };
        Dispatcher.UIThread.Post(() =>
        {
            DialogServerNameBox.Focus();
            DialogServerNameBox.SelectAll();
        });
    }

    void HideServerDialog()
    {
        editingServer = "";
        ServerDialogLayer.IsVisible = false;
        PageRoot.Effect = null;
        DialogServerNameBox.Text = "";
        DialogServerAddressBox.Text = "";
        dialogInstance = "";
        dialogShowInstances = false;
        DialogRootList.Children.Clear();
        DialogInstanceList.Children.Clear();
        DialogInstanceButton.Flyout?.Hide();
        SetDialogStatus("");
    }

    /// <summary>打开弹窗时按当前绑定重建下拉按钮与两级列表。</summary>
    void FillDialogInstances(string selected)
    {
        dialogInstance = selected.Trim();
        // 绑定了实例才展开右侧实例列；「不绑定」时只留目录栏
        dialogShowInstances = dialogInstance.Length > 0;
        RenderDialogPickers();
    }

    void RenderDialogPickers()
    {
        UpdateDialogInstanceButton();
        RenderDialogRoots();
        RenderDialogInstances();
        UpdateDialogColumns();
    }

    /// <summary>右侧「该目录下的实例」列随「不绑定实例」的选中状态收起 / 展开。</summary>
    void UpdateDialogColumns()
    {
        DialogPickerGrid.ColumnDefinitions[2].Width = dialogShowInstances ? new GridLength(300) : new GridLength(0);
        DialogPickerDivider.IsVisible = dialogShowInstances;
        DialogInstanceColumn.IsVisible = dialogShowInstances;
    }

    /// <summary>下拉按钮正面：当前绑定的图标、名称与「版本类型 版本号，加载器」。</summary>
    void UpdateDialogInstanceButton()
    {
        if (dialogInstance.Length == 0)
        {
            SetDialogInstanceFace(null, "不绑定实例", "只能手动进入服务器", "#C9D2DA", "–");
            return;
        }

        var info = Instances().FirstOrDefault(item =>
            item.Name.Equals(dialogInstance, StringComparison.OrdinalIgnoreCase));
        if (info is null)
        {
            SetDialogInstanceFace(null, dialogInstance, "当前目录下找不到这个实例", "#A5483F", "!");
            return;
        }

        var row = InstanceListItem.From(info);
        SetDialogInstanceFace(row.Icon, row.Title, row.Subtitle, "#C9D2DA", row.Glyph, row.Accent);
    }

    void SetDialogInstanceFace(Bitmap? icon, string title, string detail, string fallbackColor, string glyph,
        IBrush? accent = null)
    {
        DialogInstanceIcon.Background = icon is not null
            ? Brushes.Transparent
            : accent ?? new SolidColorBrush(Color.Parse(fallbackColor));
        DialogInstanceIcon.ClipToBounds = icon is not null;
        DialogInstanceGlyph.IsVisible = icon is null;
        DialogInstanceGlyph.Text = glyph;
        DialogInstanceImage.Source = icon;
        DialogInstanceImage.IsVisible = icon is not null;
        DialogInstanceTitle.Text = title;
        DialogInstanceDetail.Text = detail;
    }

    /// <summary>一级：实例目录。「不绑定实例」固定排在第一项，选中时右侧实例列收起。</summary>
    void RenderDialogRoots()
    {
        DialogRootList.Children.Clear();
        if (state is null)
            return;
        DialogRootList.Children.Add(DialogUnboundChoice(dialogInstance.Length == 0));
        foreach (var root in state.GameRoots)
            DialogRootList.Children.Add(
                DialogRootChoice(root, root.Equals(state.Root, StringComparison.OrdinalIgnoreCase)));
    }

    Control DialogUnboundChoice(bool active)
    {
        var lines = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        lines.Children.Add(new TextBlock
        {
            Text = "不绑定实例",
            FontSize = 11.5,
            FontWeight = active ? FontWeight.SemiBold : FontWeight.Normal,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        lines.Children.Add(new TextBlock
        {
            Text = "只手动进入服务器",
            FontSize = 9.5,
            Classes = { "muted" },
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        var row = new Button
        {
            Content = lines,
            Classes = { "instanceRow" },
            Padding = new Thickness(6, 4),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        if (active)
            row.Classes.Set("selected", true);
        row.Click += (_, _) =>
        {
            dialogInstance = "";
            dialogShowInstances = false;
            UpdateDialogInstanceButton();
            UpdateDialogColumns();
            DialogInstanceButton.Flyout?.Hide();
        };
        return row;
    }

    Control DialogRootChoice(string path, bool active)
    {
        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (name.Length == 0)
            name = path;
        // 两行：目录名 + 绝对路径。路径被列宽截断，完整值仍由 ToolTip 兜底。
        var lines = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        lines.Children.Add(new TextBlock
        {
            Text = name,
            FontSize = 11.5,
            FontWeight = active ? FontWeight.SemiBold : FontWeight.Normal,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        lines.Children.Add(new TextBlock
        {
            Text = path,
            FontSize = 9.5,
            Classes = { "muted" },
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        var row = new Button
        {
            Content = lines,
            Classes = { "instanceRow" },
            Padding = new Thickness(6, 4),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        if (active)
            row.Classes.Set("selected", true);
        ToolTip.SetTip(row, path);
        row.Click += async (_, _) =>
        {
            // 点目录总是展开右侧实例列；只有真的换目录时才写配置
            dialogShowInstances = true;
            UpdateDialogColumns();
            if (!active)
            {
                SetDialogStatus("正在切换到 " + name + "…");
                var result = await service!.SaveSettingsAsync(new AppSettingsPatch(Root: path));
                SetDialogStatus(result.Message);
            }

            // 指定目录后默认落到该目录下的第一个实例
            dialogInstance = Instances().FirstOrDefault()?.Name ?? "";
            UpdateDialogInstanceButton();
            RenderDialogInstances();
        };
        return row;
    }

    /// <summary>二级：当前目录下的实例。「不绑定实例」已经放在目录栏，这里不再重复。</summary>
    void RenderDialogInstances()
    {
        DialogInstanceList.Children.Clear();
        var instances = Instances();
        foreach (var info in instances)
        {
            var row = InstanceListItem.From(info);
            DialogInstanceList.Children.Add(DialogInstanceChoice(
                row, row.Instance.Equals(dialogInstance, StringComparison.OrdinalIgnoreCase)));
        }

        // 记着的实例在当前目录里不存在时补一行，避免保存时被悄悄清空
        if (dialogInstance.Length > 0 &&
            !instances.Any(item => item.Name.Equals(dialogInstance, StringComparison.OrdinalIgnoreCase)))
            DialogInstanceList.Children.Add(DialogInstanceChoice(InstanceListItem.Missing(dialogInstance), true));
        else if (instances.Count == 0)
            DialogInstanceList.Children.Add(Muted("这个目录下还没有实例。"));
    }

    Control DialogInstanceChoice(InstanceListItem row, bool active)
    {
        var icon = new Border
        {
            Width = 22,
            Height = 22,
            CornerRadius = new CornerRadius(6),
            VerticalAlignment = VerticalAlignment.Center
        };
        if (row.Icon is { } bitmap)
        {
            icon.ClipToBounds = true;
            icon.Child = new Image { Source = bitmap, Stretch = Stretch.Uniform };
        }
        else
        {
            icon.Background = row.Accent;
            icon.Child = new TextBlock
            {
                Text = row.Glyph,
                Foreground = Brushes.White,
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        var lines = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        lines.Children.Add(new TextBlock
        {
            Text = row.Title,
            FontSize = 11.5,
            FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        if (row.HasSubtitle)
            lines.Children.Add(new TextBlock
            {
                Text = row.Subtitle,
                FontSize = 10,
                Classes = { "muted" },
                TextTrimming = TextTrimming.CharacterEllipsis
            });

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 8 };
        grid.Children.Add(icon);
        Grid.SetColumn(lines, 1);
        grid.Children.Add(lines);

        var button = new Button
        {
            Content = grid,
            Classes = { "instanceRow" },
            Padding = new Thickness(6, 4),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        if (active)
            button.Classes.Set("selected", true);
        button.Click += (_, _) =>
        {
            dialogInstance = row.Instance;
            UpdateDialogInstanceButton();
            DialogInstanceButton.Flyout?.Hide();
        };
        return button;
    }

    string SelectedDialogInstance() => dialogInstance;

    async Task SaveServerFromDialog()
    {
        var address = DialogServerAddressBox.Text?.Trim() ?? "";
        if (address.Length == 0)
        {
            SetDialogStatus("请填写服务器网页。");
            DialogServerAddressBox.Focus();
            return;
        }

        SetDialogStatus("正在保存…");
        DialogSaveButton.IsEnabled = false;
        try
        {
            var result = await service!.SaveServerAsync(
                DialogServerNameBox.Text?.Trim() ?? "",
                address,
                editingServer,
                SelectedDialogInstance());
            if (result.Ok)
            {
                HideServerDialog();
                Status(result.Message);
                return;
            }

            SetDialogStatus(result.Message);
        }
        finally
        {
            DialogSaveButton.IsEnabled = true;
        }
    }

    void OnDialogTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        e.Handled = true;
        _ = SaveServerFromDialog();
    }

    void SetDialogStatus(string message)
    {
        ServerDialogStatus.Text = message;
        ServerDialogStatus.IsVisible = message.Length > 0;
    }

    void Status(string message) => HomeStatus.Text = message;

    static TextBlock Muted(string text) => new() { Text = text, Classes = { "muted" } };
}
