using Avalonia.Controls;
using MCUGLauncher.Avalonia.Presentation;
namespace MCUGLauncher.Avalonia.Views;

// One home feed card per original dashboard region; no merged replacement page.
internal sealed class HomeFeedPanel : NativePage
{
    readonly string kind;
    readonly Dictionary<string, NativeMessage> pings = [];
    int index;
    readonly HashSet<string> queried = [];
    public HomeFeedPanel(LauncherFacade session, string kind) : base(session)
    {
        this.kind = kind;
        Draw();
        PingMissing();
    }
    void PingMissing()
    {
        if (kind != "servers")
            return;
        foreach (var server in State.Servers)
            if (queried.Add(server.Address))
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() => { _ = Send(new { type = "pingServer", address = server.Address, id = server.Address }); });
    }
    void Draw()
    {
        var card = Ui.Card(kind switch
        {
            "servers" => Servers(),
            "announcements" => Announcements(),
            "recommendations" => Recommendations(),
            _ => Quote()
        });
        card.Margin = default;
        card.Padding = new global::Avalonia.Thickness(15, 12);
        if (kind == "quotes")
            card.Height = 100;
        Content = card;
    }
    static Grid Header(string title, Control actions)
    {
        var label = Ui.Text(title, 13.5);
        label.FontWeight = global::Avalonia.Media.FontWeight.SemiBold;
        label.VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center;
        return Ui.Beside(label, actions);
    }
    static Button Link(string label, Action action)
    {
        var button = Ui.Action(label, action);
        button.Classes.Add("link");
        return button;
    }
    static Button Link(string label, Func<Task> action)
    {
        var button = Ui.Button(label, action);
        button.Classes.Add("link");
        return button;
    }
    Grid Servers()
    {
        var panel = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), RowSpacing = 10 };
        var tools = Ui.Row(Link("‹", () => { index--; Draw(); }), Link("›", () => { index++; Draw(); }), Link("添加服务器", () => ServerDialog.Open(Session, Owner)));
        panel.Children.Add(Header("正在开设的服务器", tools));
        var items = State.Servers;
        Control body;
        if (items.Length == 0)
        {
            var copy = Ui.Stack(Ui.Text("还没有添加服务器", 12), Ui.Muted("填入 host:port，就能在这里看到实时人数与延迟。"));
            body = Ui.Beside(copy, Link("＋ 添加", () => ServerDialog.Open(Session, Owner)));
            body.VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center;
        }
        else
        {
            var server = items[((index % items.Length) + items.Length) % items.Length];
            var status = pings.GetValueOrDefault(server.Address);
            body = Ui.Stack(Ui.Text(server.Name, 15), Ui.Muted(server.Address),
                Ui.Text(status == null ? "正在查询状态…" : status.Online ? $"在线 · {status.Players}/{status.Max} 人 · {status.Ping} ms" : "离线或暂时无法连接", 11),
                Ui.Row(Link("刷新状态", () => Send(new { type = "pingServer", address = server.Address, id = server.Address })),
                    Link("编辑服务器", () => ServerDialog.Open(Session, Owner, server)),
                    Link("移除", async () => { if (await Ui.Confirm(Owner, "移除服务器", "移除这个收藏的服务器？")) await Send(new { type = "deleteServer", address = server.Address }); })));
        }
        Grid.SetRow(body, 1);
        panel.Children.Add(body);
        return panel;
    }
    CommunityEntry[] Entries(string key) => (Session.Community.GetValueOrDefault(key) ?? []).Where(e => !DateTimeOffset.TryParse(e.ExpiresAt, out var expiry) || expiry > DateTimeOffset.Now).ToArray();
    Grid Announcements()
    {
        var panel = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), RowSpacing = 8 };
        panel.Children.Add(Header("群公告", Link("查看全部", () => Ui.Show(Owner, "全部公告", new CommunityPage(Session, onlyFeed: "announcements")))));
        var items = Entries("announcements");
        Control body;
        if (items.Length == 0)
            body = Ui.Muted("暂无公告，新的消息会出现在这里。");
        else
        {
            var entry = items[Math.Abs(index) % items.Length];
            var title = Ui.Text(entry.Title, 12);
            title.FontWeight = global::Avalonia.Media.FontWeight.SemiBold;
            var summary = Ui.Muted(entry.Body.Length > 0 ? entry.Body : entry.Text);
            summary.MaxLines = 2;
            summary.TextTrimming = global::Avalonia.Media.TextTrimming.CharacterEllipsis;
            var copy = Ui.Stack(title, summary);
            copy.Spacing = 4;
            var pager = new StackPanel { Orientation = global::Avalonia.Layout.Orientation.Horizontal, Spacing = 4, VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center };
            pager.Children.Add(Link("‹", () => { index = (index + items.Length - 1) % items.Length; Draw(); }));
            pager.Children.Add(Ui.Muted($"{index % items.Length + 1} / {items.Length}"));
            pager.Children.Add(Link("›", () => { index++; Draw(); }));
            var row = Ui.Beside(copy, pager);
            if (entry.ImageUrl.Length > 0)
            {
                var image = new RemoteImage(Session, entry.ImageUrl, 48);
                var illustrated = new Grid { ColumnDefinitions = new ColumnDefinitions("48,*"), ColumnSpacing = 10 };
                illustrated.Children.Add(image);
                Grid.SetColumn(row, 1);
                illustrated.Children.Add(row);
                body = illustrated;
            }
            else
                body = row;
        }
        Grid.SetRow(body, 1);
        panel.Children.Add(body);
        return panel;
    }
    Grid Recommendations()
    {
        var panel = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), RowSpacing = 10 };
        panel.Children.Add(Header("群友推荐", Link("查看更多", () => Session.Navigate?.Invoke("community"))));
        var rows = Ui.Stack();
        rows.Spacing = 6;
        foreach (var entry in Entries("recommendations").Take(3))
        {
            var copy = Ui.Stack(Ui.Text(entry.Name, 12), Ui.Muted("由 " + entry.Author + " 推荐", 10));
            copy.Spacing = 3;
            var button = Ui.Button("", () => Recommendation(Session, Owner, entry));
            button.Content = Ui.Beside(copy, Ui.Muted("›", 16));
            button.HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch;
            button.HorizontalContentAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch;
            button.Padding = new global::Avalonia.Thickness(8, 5);
            button.Classes.Add("link");
            rows.Children.Add(button);
        }
        if (rows.Children.Count == 0)
        {
            rows.Children.Add(Ui.Text("暂无群友推荐", 12));
            rows.Children.Add(Ui.Muted("等群友整理好，第一时间放上来。"));
            rows.VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center;
        }
        Grid.SetRow(rows, 1);
        panel.Children.Add(rows);
        return panel;
    }
    Grid Quote()
    {
        var entries = Entries("quotes");
        var entry = entries.Length > 0 ? entries[Math.Abs(index) % entries.Length] : null;
        var copy = Ui.Text(entry == null ? "还没有语录。" : entry.Text + " —— " + entry.Author, 15);
        copy.MaxLines = 2;
        copy.TextTrimming = global::Avalonia.Media.TextTrimming.CharacterEllipsis;
        copy.VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center;
        Control main = copy;
        if (entry?.ImageUrl.Length > 0)
        {
            var imageRow = new Grid { ColumnDefinitions = new ColumnDefinitions("136,*"), ColumnSpacing = 15 };
            imageRow.Children.Add(new RemoteImage(Session, entry.ImageUrl, 70) { Width = 136 });
            Grid.SetColumn(copy, 1);
            imageRow.Children.Add(copy);
            main = imageRow;
        }
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("68,*,Auto"), ColumnSpacing = 15 };
        var label = Ui.Stack(Ui.Muted("“", 26), Ui.Text("群友语录", 10.5));
        label.Spacing = 0;
        label.VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center;
        row.Children.Add(label);
        Grid.SetColumn(main, 1);
        row.Children.Add(main);
        var next = Link("下一条", () => { index++; Draw(); });
        next.VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center;
        Grid.SetColumn(next, 2);
        row.Children.Add(next);
        return row;
    }
    internal static Task Recommendation(LauncherFacade session, Window owner, CommunityEntry entry)
    {
        if (entry.Type == "server")
            return ServerDialog.Open(session, owner, new NativeItem { Name = entry.Name, Address = entry.Address.Length > 0 ? entry.Address : entry.Addr }, false);
        session.PendingSearch = entry.Name;
        session.Navigate?.Invoke(entry.Type == "shader" ? "shader" : "resourcepack");
        return Task.CompletedTask;
    }
    protected override void Receive(NativeMessage message)
    {
        if (kind == "servers" && message.Type == "serverPing")
        {
            pings[message.Id] = message;
            Draw();
        }
        if (message.Type is "state" or "community")
        {
            Draw();
            PingMissing();
        }
    }
}
internal static class ServerDialog
{
    public static Task Open(LauncherFacade session, Window owner, NativeItem? server = null, bool editing = true)
    {
        var name = Ui.Input(server?.Name ?? "");
        var address = Ui.Input(server?.Address ?? "");
        var content = Ui.Stack(Ui.Heading(server == null ? "添加服务器" : "编辑服务器"), Ui.Field("名称", name), Ui.Field("地址", address));
        content.Children.Add(Ui.Button("保存服务器", async () =>
        {
            var response = await session.RequestAsync("serverResult", new
            {
                type = "saveServer",
                name = name.Text ?? "",
                address = address.Text ?? "",
                oldAddress = editing ? server?.Address ?? "" : ""
            });
            if (response.Ok)
                (TopLevel.GetTopLevel(content) as Window)?.Close();
            else
                throw new ArgumentException(response.Message);
        }, true));
        return Ui.Show(owner, server == null ? "添加服务器" : "服务器", content, 520, 420);
    }
}
