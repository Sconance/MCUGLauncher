using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using MCUGLauncher.Contracts;

namespace MCUGLauncher.Avalonia.Views;

internal partial class HomeView : UserControl
{
    readonly Dictionary<string, ServerPingInfo> pings = new(StringComparer.OrdinalIgnoreCase);
    ILauncherService? service;
    LauncherState? state;
    CommunityData community = CommunityData.Empty;
    Bitmap? announcementImage;
    Bitmap? avatar;
    int imageVersion;
    string serverSignature = "";
    string editingServer = "";
    bool updatingInstances;
    int announcementIndex;
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

    public void Attach(ILauncherService value)
    {
        service = value;
        service.StateChanged += ApplyState;
        service.CommunityChanged += OnCommunity;
        service.AvatarReceived += OnAvatar;
        service.ServerPingReceived += OnServerPing;
        service.ServerResultReceived += OnServerResult;
        service.ToastReceived += info => Status(info.Message);
        service.ErrorReceived += message => Status("操作失败：" + message);
        community = service.Community;
        ApplyState(service.State);
        RenderCommunity();
    }

    /// <summary>账号恢复完成后由外壳调用，避免在 ready 之前读取头像。</summary>
    public void Activate() => _ = service?.ReadAvatarAsync();

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
            await service!.LaunchAsync(target);
        };
        RefreshButton.Click += async (_, _) => await service!.RequestStateAsync();
        InstanceSelector.SelectionChanged += async (_, _) =>
        {
            if (updatingInstances || state is null)
                return;
            var instances = Instances();
            if (InstanceSelector.SelectedIndex < 0 || InstanceSelector.SelectedIndex >= instances.Count)
                return;
            var name = instances[InstanceSelector.SelectedIndex].Name;
            if (name != state.SelectedInstance)
                await service!.SelectInstanceAsync(name);
        };
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
            await service!.SaveServerAsync(ServerNameBox.Text?.Trim() ?? "", address, editingServer);
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
            _ = service?.ReadAvatarAsync();
        }
        var current = snapshot.Instances.FirstOrDefault(item => item.Name == snapshot.SelectedInstance)
            ?? snapshot.Instances.FirstOrDefault(item => item.Name == snapshot.SelectedVersion);
        LaunchTarget.Text = current is null
            ? "请选择实例"
            : $"{current.DisplayName} · {current.Minecraft} · {current.Loader}";
        LaunchSubtitle.Text = current is null
            ? "未选择实例"
            : $"{current.Name} · " + (snapshot.AutoMemory ? "自动内存" : snapshot.Ram + " MB");
        LaunchButton.IsEnabled = current is not null;
        RenderInstances();
        var signature = string.Join("|", snapshot.Servers.Select(item => item.Address));
        if (signature != serverSignature)
        {
            serverSignature = signature;
            pings.Clear();
            foreach (var server in snapshot.Servers)
                if (server.Address.Length > 0)
                    _ = service!.PingServerAsync(server.Address, server.Address);
        }
        RenderServers();
    }

    void RenderInstances()
    {
        if (state is null)
            return;
        var instances = Instances();
        var labels = instances
            .Select(item => item.DisplayName + " · " + item.Loader + " · " + item.Minecraft)
            .ToArray();
        updatingInstances = true;
        InstanceSelector.ItemsSource = labels;
        var index = Array.FindIndex(instances.ToArray(), item => item.Name == state.SelectedInstance);
        InstanceSelector.SelectedIndex = labels.Length == 0 ? -1 : Math.Max(0, index);
        updatingInstances = false;
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
                Child = new TextBlock
                {
                    Text = "▣",
                    Foreground = Brushes.White,
                    FontSize = 16,
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
                Text = ping is null ? "等待查询" : ping.Online ? ping.Motd : "无法连接",
                TextWrapping = TextWrapping.Wrap,
                Classes = { "muted" }
            });
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            var current = server;
            var query = new Button { Content = "查询", Classes = { "link" } };
            query.Click += async (_, _) => await service!.PingServerAsync(current.Address, current.Address);
            var edit = new Button { Content = "编辑", Classes = { "link" } };
            edit.Click += (_, _) => ShowServerForm(current);
            var delete = new Button { Content = "删除", Classes = { "link" } };
            delete.Click += async (_, _) =>
            {
                await service!.DeleteServerAsync(current.Address);
                pings.Remove(current.Address);
                Status("已删除 " + current.Address);
            };
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
            _ = ShowAnnouncementImage("");
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
        _ = ShowAnnouncementImage(item.ImageUrl);
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
                Child = new TextBlock
                {
                    Text = item.Type switch { "material" => "◆", "shader" => "✦", _ => "▣" },
                    Foreground = Brushes.White,
                    FontSize = 13,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            row.Children.Add(icon);
            Grid.SetColumn(copy, 1);
            copy.Margin = new Thickness(9, 0, 0, 0);
            row.Children.Add(copy);
            RecommendationPanel.Children.Add(new Border
            {
                BorderBrush = new SolidColorBrush(Color.Parse("#E7EEF4")),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(0, 6),
                Child = row
            });
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

    void OnAvatar(AvatarInfo info)
    {
        avatar?.Dispose();
        avatar = Decode(info.Image);
        AvatarImage.Source = avatar;
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

    static TextBlock Muted(string text) => new() { Text = text, Classes = { "muted" } };
}
