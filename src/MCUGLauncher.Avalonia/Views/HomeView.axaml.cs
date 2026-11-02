using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace MCUGLauncher.Avalonia.Views;

internal partial class HomeView : UserControl
{
    readonly Dictionary<string, ServerPingInfo> pings = new(StringComparer.OrdinalIgnoreCase);
    LauncherFacade? facade;
    LauncherState? state;
    CommunityData community = CommunityData.Empty;
    Bitmap? announcementImage;
    string serverSignature = "";
    string editingServer = "";
    int announcementIndex;
    int quoteIndex;

    public HomeView()
    {
        InitializeComponent();
        WireButtons();
    }

    public HomeView(LauncherFacade facade) : this()
    {
        Attach(facade);
    }

    public event Action? AccountRequested;

    public void Attach(LauncherFacade value)
    {
        facade = value;
        facade.StateChanged += ApplyState;
        facade.CommunityChanged += OnCommunity;
        facade.ServerPingReceived += OnServerPing;
        facade.ServerResultReceived += OnServerResult;
        facade.ToastReceived += info => Status(info.Message);
        facade.ErrorReceived += message => Status("操作失败：" + message);
        community = facade.Community;
        ApplyState(facade.State);
        RenderCommunity();
    }

    void WireButtons()
    {
        AccountButton.Click += (_, _) => AccountRequested?.Invoke();
        LaunchButton.Click += async (_, _) =>
        {
            var target = state?.SelectedInstance is { Length: > 0 } instance ? instance : state?.SelectedVersion ?? "";
            if (target.Length == 0)
            {
                Status("请先安装或选择实例。");
                return;
            }
            Status("正在准备启动…");
            await facade!.LaunchAsync(target);
        };
        RefreshButton.Click += async (_, _) => await facade!.RequestStateAsync();
        AddServerButton.Click += (_, _) =>
        {
            editingServer = "";
            ServerNameBox.Text = "";
            ServerAddressBox.Text = "";
            ServerForm.IsVisible = true;
            ServerStatus.Text = "";
        };
        CancelServerButton.Click += (_, _) => HideServerForm();
        SaveServerButton.Click += async (_, _) =>
        {
            var address = ServerAddressBox.Text?.Trim() ?? "";
            if (address.Length == 0)
            {
                ServerStatus.Text = "请填写服务器地址。";
                return;
            }
            ServerStatus.Text = "正在保存…";
            await facade!.SaveServerAsync(ServerNameBox.Text?.Trim() ?? "", address, editingServer);
        };
        PrevAnnouncementButton.Click += (_, _) =>
        {
            if (community.Announcements.Count == 0)
                return;
            announcementIndex = (announcementIndex - 1 + community.Announcements.Count) % community.Announcements.Count;
            RenderAnnouncement();
        };
        NextAnnouncementButton.Click += (_, _) =>
        {
            if (community.Announcements.Count == 0)
                return;
            announcementIndex = (announcementIndex + 1) % community.Announcements.Count;
            RenderAnnouncement();
        };
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
            await facade!.RefreshCommunityAsync();
        };
    }

    void ApplyState(LauncherState? snapshot)
    {
        state = snapshot;
        if (snapshot is null)
            return;
        PlayerName.Text = snapshot.Player.Length > 0 ? snapshot.Player : "未登录";
        AccountMeta.Text = snapshot.AccountLabel.Length > 0 ? snapshot.AccountLabel : "离线账号 · 本地启动";
        var current = snapshot.Instances.FirstOrDefault(item => item.Name == snapshot.SelectedInstance)
            ?? snapshot.Instances.FirstOrDefault(item => item.Name == snapshot.SelectedVersion);
        LaunchTarget.Text = current is null
            ? "请选择实例"
            : $"{current.DisplayName} · {snapshot.SelectedVersion} · "
              + (snapshot.AutoMemory ? "自动内存" : snapshot.Ram + " MB");
        LaunchButton.IsEnabled = current is not null;
        RenderInstances();
        var signature = string.Join("|", snapshot.Servers.Select(item => item.Address));
        if (signature != serverSignature)
        {
            serverSignature = signature;
            pings.Clear();
            foreach (var server in snapshot.Servers)
                if (server.Address.Length > 0)
                    _ = facade!.PingServerAsync(server.Address, server.Address);
        }
        RenderServers();
    }

    void RenderInstances()
    {
        InstancePanel.Children.Clear();
        if (state is null)
            return;
        var instances = state.Instances
            .Where(item => item.Name.Length > 0 && item.Loader != "请安装版本")
            .ToArray();
        if (instances.Length == 0)
        {
            InstancePanel.Children.Add(Muted("尚未找到已安装实例。"));
            return;
        }
        foreach (var instance in instances)
        {
            var selected = instance.Name == state.SelectedInstance;
            var copy = new StackPanel { Spacing = 2 };
            copy.Children.Add(new TextBlock
            {
                Text = instance.DisplayName + (selected ? "  · 当前" : ""),
                FontWeight = FontWeight.SemiBold
            });
            copy.Children.Add(new TextBlock
            {
                Text = $"{instance.Minecraft} · {instance.Loader} · {instance.Mods} 个模组",
                Classes = { "muted" }
            });
            var launch = new Button { Content = "启动", Classes = { "ghost" } };
            var name = instance.Name;
            launch.Click += async (_, _) => await facade!.LaunchAsync(name);
            var select = new Button
            {
                Content = selected ? "当前实例" : "设为当前",
                Classes = { "ghost" },
                IsEnabled = !selected
            };
            select.Click += async (_, _) => await facade!.SelectInstanceAsync(name);
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            actions.Children.Add(select);
            actions.Children.Add(launch);
            var row = new Border
            {
                Background = selected ? new SolidColorBrush(Color.Parse("#EAF4FB")) : Brushes.Transparent,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 6),
                Child = Pair(copy, actions)
            };
            InstancePanel.Children.Add(row);
        }
    }

    void RenderServers()
    {
        ServerPanel.Children.Clear();
        if (state is not { Servers.Count: > 0 })
        {
            ServerPanel.Children.Add(Muted("还没有收藏服务器。填入 host:port，就能看到实时人数与延迟。"));
            ServerSummary.Text = "0 台服务器";
            return;
        }
        var online = state.Servers.Count(server => pings.TryGetValue(server.Address, out var ping) && ping.Online);
        ServerSummary.Text = $"{online} 在线 · {state.Servers.Count - online} 离线";
        foreach (var server in state.Servers)
        {
            pings.TryGetValue(server.Address, out var ping);
            var copy = new StackPanel { Spacing = 3 };
            copy.Children.Add(new TextBlock
            {
                Text = server.Name.Length > 0 ? server.Name : server.Address,
                FontWeight = FontWeight.SemiBold
            });
            copy.Children.Add(new TextBlock { Text = server.Address, Classes = { "muted" } });
            copy.Children.Add(new TextBlock
            {
                Text = ping is null
                    ? "尚未查询"
                    : ping.Online
                        ? $"{ping.Players}/{ping.Max} 人 · {ping.Ping} ms · {ping.Motd}"
                        : "离线 · " + ping.Error,
                TextWrapping = TextWrapping.Wrap,
                Classes = { "muted" }
            });
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            var current = server;
            var query = new Button { Content = "查询", Classes = { "ghost" } };
            query.Click += async (_, _) => await facade!.PingServerAsync(current.Address, current.Address);
            var edit = new Button { Content = "编辑", Classes = { "ghost" } };
            edit.Click += (_, _) => ShowServerForm(current);
            var delete = new Button { Content = "删除", Classes = { "ghost" } };
            delete.Click += async (_, _) =>
            {
                await facade!.DeleteServerAsync(current.Address);
                pings.Remove(current.Address);
                Status("已删除 " + current.Address);
            };
            actions.Children.Add(query);
            actions.Children.Add(edit);
            actions.Children.Add(delete);
            ServerPanel.Children.Add(new Border
            {
                Width = 260,
                Margin = new Thickness(0, 0, 10, 10),
                Padding = new Thickness(12),
                CornerRadius = new CornerRadius(10),
                Background = new SolidColorBrush(Color.Parse("#F8FAFC")),
                Child = new StackPanel { Spacing = 6, Children = { copy, actions } }
            });
        }
    }

    void OnCommunity(CommunityData data)
    {
        community = data;
        announcementIndex = Math.Clamp(announcementIndex, 0, Math.Max(0, data.Announcements.Count - 1));
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
        PrevAnnouncementButton.IsEnabled = community.Announcements.Count > 0;
        NextAnnouncementButton.IsEnabled = community.Announcements.Count > 0;
        if (community.Announcements.Count == 0)
        {
            AnnouncementTitle.Text = "暂无公告";
            AnnouncementBody.Text = "新的群公告会出现在这里。";
            AnnouncementMeta.Text = "";
            AnnouncementIndex.Text = "0 / 0";
            ShowAnnouncementImage("");
            return;
        }
        var item = community.Announcements[announcementIndex];
        AnnouncementTitle.Text = "[" + item.Tag + "] " + item.Title;
        AnnouncementBody.Text = item.Body;
        AnnouncementMeta.Text = string.Join(" · ", new[]
        {
            item.Author,
            item.PublishedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "",
            item.ImageUrl.Length > 0 ? "含配图" : ""
        }.Where(value => value.Length > 0));
        AnnouncementIndex.Text = $"{announcementIndex + 1} / {community.Announcements.Count}";
        ShowAnnouncementImage(item.ImageUrl);
    }

    void ShowAnnouncementImage(string url)
    {
        announcementImage?.Dispose();
        announcementImage = null;
        AnnouncementImage.Source = null;
        AnnouncementImage.IsVisible = false;
        if (!url.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            return;
        try
        {
            var comma = url.IndexOf(',');
            if (comma < 0)
                return;
            var bytes = Convert.FromBase64String(url[(comma + 1)..]);
            using var input = new MemoryStream(bytes);
            announcementImage = new Bitmap(input);
            AnnouncementImage.Source = announcementImage;
            AnnouncementImage.IsVisible = true;
        }
        catch
        {
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
                Text = $"{type} · 由 {item.Author} 推荐" + (item.Tags.Count > 0 ? " · " + string.Join(" / ", item.Tags) : ""),
                Classes = { "muted" }
            });
            if (item.Description.Length > 0)
                copy.Children.Add(new TextBlock { Text = item.Description, TextWrapping = TextWrapping.Wrap, Classes = { "muted" } });
            RecommendationPanel.Children.Add(copy);
        }
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

    void OnServerPing(ServerPingInfo info)
    {
        pings[info.Id] = info;
        RenderServers();
    }

    void OnServerResult(ServerResultInfo info)
    {
        ServerStatus.Text = info.Message;
        if (info.Ok)
            HideServerForm();
    }

    void ShowServerForm(SavedServerInfo server)
    {
        editingServer = server.Address;
        ServerNameBox.Text = server.Name;
        ServerAddressBox.Text = server.Address;
        ServerForm.IsVisible = true;
        ServerStatus.Text = "正在编辑 " + server.Address;
    }

    void HideServerForm()
    {
        editingServer = "";
        ServerForm.IsVisible = false;
        ServerNameBox.Text = "";
        ServerAddressBox.Text = "";
        ServerStatus.Text = "";
    }

    void Status(string message) => HomeStatus.Text = message;

    static Control Pair(Control copy, Control actions)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        row.Children.Add(copy);
        Grid.SetColumn(actions, 1);
        actions.Margin = new Thickness(12, 0, 0, 0);
        actions.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(actions);
        return row;
    }

    static TextBlock Muted(string text) => new() { Text = text, Classes = { "muted" } };
}
