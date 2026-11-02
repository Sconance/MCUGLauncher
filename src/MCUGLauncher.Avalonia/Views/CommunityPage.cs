using System.Diagnostics;
using Avalonia.Controls;
using MCUGLauncher.Avalonia.Presentation;

namespace MCUGLauncher.Avalonia.Views;
internal sealed class CommunityPage : NativePage
{
    readonly StackPanel entries = Ui.Stack();
    readonly bool recommendationsOnly;
    readonly string? onlyFeed;
    readonly Dictionary<string, Button> categoryTabs = [];
    string recommendationType = "server";
    public CommunityPage(LauncherFacade session, bool recommendationsOnly = false, string? onlyFeed = null) : base(session)
    {
        this.recommendationsOnly = recommendationsOnly;
        this.onlyFeed = onlyFeed;
        Title(onlyFeed == "announcements" ? "全部公告" : recommendationsOnly ? "群友推荐" : "社区内容", "浏览群友推荐的服务器、资源包和光影。");
        Body.Children.Add(Ui.Row(
                Ui.Button("刷新社区内容", () => Send(new { type = "refreshCommunity" })),
                Ui.Action("管理服务器收藏", () => Session.Navigate?.Invoke("servers"))
        ));
        if (recommendationsOnly)
        {
            var tabs = Ui.Row();
            foreach (var (label, kind) in new[] { ("服务器", "server"), ("材质包", "material"), ("光影", "shader") })
            {
                var tab = Ui.Action(label, () => { recommendationType = kind; Draw(); });
                tab.Classes.Add("filter-tab");
                categoryTabs[kind] = tab;
                tabs.Children.Add(tab);
            }
            Body.Children.Add(tabs);
        }
        Body.Children.Add(entries);
        Draw();
    }

    void Draw()
    {
        entries.Children.Clear();
        foreach (var (kind, tab) in categoryTabs)
            tab.Classes.Set("primary", kind == recommendationType);
        foreach (var key in (onlyFeed != null ? new[] { onlyFeed } : recommendationsOnly ? new[] { "recommendations" } : new[]
            {
                "announcements",
                "recommendations",
                "quotes"
            })

        )
        {
            if (!recommendationsOnly && onlyFeed == null)
                entries.Children.Add(Ui.Heading(key switch
                {
                    "announcements" => "公告",
                    "recommendations" => "群友推荐",
                    _ => "群友语录"
                }));
            foreach (var entry in (Session.Community.GetValueOrDefault(key) ?? []).Where(e => !recommendationsOnly || e.Type == recommendationType))
            {
                if (DateTimeOffset.TryParse(entry.ExpiresAt, out var expiry) && expiry < DateTimeOffset.Now)
                    continue;
                var content = Ui.Stack(
                    Ui.Text(entry.Title.Length > 0 ? entry.Title : entry.Name, 17),
                    Ui.Text(entry.Body.Length > 0 ? entry.Body : entry.Text.Length > 0 ? entry.Text : entry.Description),
                    Ui.Text(entry.Author + " · " + entry.PublishedAt)
                );
                if (entry.ImageUrl.Length > 0)
                    content.Children.Add(new RemoteImage(Session, entry.ImageUrl));
                if (Uri.TryCreate(entry.Url, UriKind.Absolute, out var uri) && uri.Scheme == "https")
                    content.Children.Add(Ui.Action("在浏览器打开", () => Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true })));
                if (key == "recommendations")
                    content.Children.Add(Ui.Button(entry.Type == "server" ? "查看服务器" : "搜索资源", () => HomeFeedPanel.Recommendation(Session, Owner, entry)));
                entries.Children.Add(Ui.Card(content));
            }
        }
    }

    protected override void Receive(NativeMessage message)
    {
        if (message.Type == "community")
            Draw();
    }
}

internal sealed class ServersPage : NativePage
{
    readonly StackPanel list = Ui.Stack();
    readonly Dictionary<string, TextBlock> results = [];
    public ServersPage(LauncherFacade session) : base(session)
    {
        Title("服务器收藏", "启动器直接查询服务器状态，可复制地址、独立保存或删除。");
        Body.Children.Add(Ui.Button("添加服务器", () => ServerDialog.Open(Session, Owner)));
        Body.Children.Add(list);
        Draw();
    }

    void Draw()
    {
        list.Children.Clear();
        results.Clear();
        foreach (var server in State.Servers.Concat((Session.Community.GetValueOrDefault("servers") ?? []).Where(s => s.Address.Length > 0).Select(s => new NativeItem { Name = s.Name, Address = s.Address })).DistinctBy(s => s.Address, StringComparer.OrdinalIgnoreCase))
        {
            var status = Ui.Text("尚未测试");
            results[server.Address] = status;
            list.Children.Add(Ui.Card(Ui.Stack(
                        Ui.Text(server.Name, 17),
                        Ui.Text(server.Address),
                        status,
                        Ui.Row(
                            Ui.Button("查询状态", () => Send(new { type = "pingServer", id = server.Address, address = server.Address })),
                            Ui.Button("复制地址", async () =>
                                {
                                    if (Owner.Clipboard != null)
                                        await Owner.Clipboard.SetTextAsync(server.Address);
                                }),
                            Ui.Button("编辑", () => ServerDialog.Open(Session, Owner, server)),
                            Ui.Button("删除", () => Send(new { type = "deleteServer", address = server.Address }))
                        )
            )));
        }

        if (list.Children.Count == 0)
            list.Children.Add(Ui.Text("没有保存的服务器。"));
    }

    protected override void Receive(NativeMessage message)
    {
        if (message.Type is "state" or "community")
            Draw();
        if (message.Type == "serverPing" && results.TryGetValue(message.Id, out var result))
            result.Text = message.Online ? $"在线 · {message.Players}/{message.Max} 人 · {message.Ping} ms" : "离线或无法连接 · " + message.Error;
    }
}
