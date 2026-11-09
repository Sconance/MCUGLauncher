using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using MCUGLauncher.Contracts;

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
    int quoteIndex;
    string avatarAccount = "";

    public HomeView()
    {
        InitializeComponent();
        WireButtons();
    }

    public HomeView(ILauncherService service) : this()
    {
        Attach(service);
    }

    public event Action? AccountRequested;

    /// <summary>二级页面里点「⚙」时请求外壳切到实例管理页。</summary>
    public event Action? InstancesRequested;

    /// <summary>启动按钮右侧「▣」：请求外壳切到运行中的游戏页。</summary>
    public event Action? RunningGamesRequested;

    public void Attach(ILauncherService value)
    {
        service = value ?? EmptyServiceFallback.Instance;
        service.StateChanged += ApplyState;
        service.CommunityChanged += OnCommunity;
        service.ToastReceived += info => Status(info.Message);
        service.ErrorReceived += message => Status("操作失败：" + message);
        community = service.Community;
        ApplyState(service.State);
        RenderCommunity();
    }

    /// <summary>账号恢复完成后由外壳调用，避免在 ready 之前读取头像。</summary>
    public void Activate() => _ = ReadAvatar();

    void WireButtons()
    {
        AccountButton.Click += (_, _) => AccountRequested?.Invoke();
        AvatarButton.Click += (_, _) => AccountRequested?.Invoke();
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
        InstanceButton.Click += (_, _) => ToggleInstances(!InstancePicker.IsVisible);
        VersionSettingsButton.Click += (_, _) => InstancesRequested?.Invoke();
        RunningGameButton.Click += (_, _) => RunningGamesRequested?.Invoke();
        InstancePicker.CloseRequested += () => ToggleInstances(false);
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
        InstancePicker.SettingsRequested += async name =>
        {
            await service!.SelectInstanceAsync(name);
            ToggleInstances(false);
            InstancesRequested?.Invoke();
        };
        AddServerButton.Click += (_, _) => ShowServerDialog(null);
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
        PlayerName.Text = snapshot.Player.Length > 0 ? snapshot.Player : "未登录";
        AccountMeta.Text = AccountText.Label(snapshot.AccountType);
        var avatarKey = snapshot.AccountType + "|" + snapshot.Player;
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

    /// <summary>切换右侧信息流与实例选择二级页面；按钮右箭头随之翻转。</summary>
    void ToggleInstances(bool open)
    {
        InstancePicker.IsVisible = open;
        FeedScroll.IsVisible = !open;
        InstanceButtonChevron.Text = open ? "‹" : "›";
        if (!open)
            InstancePicker.SetStatus("");
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
                Child = new global::Avalonia.Svg.Skia.Svg(new Uri("avares://MCUGLauncher/"))
                {
                    Path = "/Assets/ServerIcon.svg",
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
            item.PublishedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "",
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
                Child = new global::Avalonia.Svg.Skia.Svg(new Uri("avares://MCUGLauncher/"))
                {
                    Path = "/Assets/" + iconFile + ".svg",
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
        DialogServerInstanceBox.SelectedIndex = -1;
        DialogServerInstanceBox.ItemsSource = null;
        SetDialogStatus("");
    }

    /// <summary>第一项是「不绑定」，其余按当前可用实例顺序排列。</summary>
    void FillDialogInstances(string selected)
    {
        var binding = selected.Trim();
        var instances = Instances();
        var items = new List<InstanceListItem> { InstanceListItem.None };
        items.AddRange(instances.Select(InstanceListItem.From));
        var known = instances.Any(item => item.Name.Equals(binding, StringComparison.OrdinalIgnoreCase));
        // 记着的实例已被删除时也留一个补位项，避免保存时把它悄悄清掉
        if (binding.Length > 0 && !known)
        {
            items.Add(InstanceListItem.Missing(binding));
        }

        DialogServerInstanceBox.ItemsSource = items;
        DialogServerInstanceBox.SelectedIndex = binding.Length > 0
            ? Math.Max(0, items.FindIndex(item => item.Instance.Equals(binding, StringComparison.OrdinalIgnoreCase)))
            : 0;
    }

    string SelectedDialogInstance() =>
        (DialogServerInstanceBox.SelectedItem as InstanceListItem)?.Instance ?? "";

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
