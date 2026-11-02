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
        Body.Children.Clear();
        Body.Children.Add(Ui.Card(kind switch
        {
            "servers" => Servers(),
            "announcements" => Announcements(),
            "recommendations" => Recommendations(),
            _ => Quote()
        }));
    }
    StackPanel Servers()
    {
        var panel = Ui.Stack(Ui.Row(Ui.Heading("正在开设的服务器"), Ui.Button("添加服务器", () => ServerDialog.Open(Session, Owner)), Ui.Action("‹", () => { index--; Draw(); }), Ui.Action("›", () => { index++; Draw(); })));
        var items = State.Servers;
        if (items.Length == 0)
        {
            panel.Children.Add(Ui.Text("还没有添加服务器，填入 host:port 即可查看实时人数和延迟。"));
            return panel;
        }
        var server = items[((index % items.Length) + items.Length) % items.Length];
        var status = pings.GetValueOrDefault(server.Address);
        panel.Children.Add(Ui.Text(server.Name, 17));
        panel.Children.Add(Ui.Text(server.Address));
        panel.Children.Add(Ui.Text(status == null ? "尚未查询状态" : status.Online ? $"在线 · {status.Players}/{status.Max} 人 · {status.Ping} ms" : "离线或暂时无法连接"));
        panel.Children.Add(Ui.Row(
            Ui.Button("刷新状态", () => Send(new { type = "pingServer", address = server.Address, id = server.Address })),
            Ui.Button("编辑服务器", () => ServerDialog.Open(Session, Owner, server)),
            Ui.Button("移除", async () => { if (await Ui.Confirm(Owner, "移除服务器", "移除这个收藏的服务器？")) await Send(new { type = "deleteServer", address = server.Address }); })));
        return panel;
    }
    CommunityEntry[] Entries(string key) => (Session.Community.GetValueOrDefault(key) ?? []).Where(e => !DateTimeOffset.TryParse(e.ExpiresAt, out var expiry) || expiry > DateTimeOffset.Now).ToArray();
    StackPanel Announcements()
    {
        var panel = Ui.Stack(Ui.Row(Ui.Heading("群公告"), Ui.Button("查看全部", () => Ui.Show(Owner, "全部公告", new CommunityPage(Session, onlyFeed: "announcements")))));
        var items = Entries("announcements");
        if (items.Length == 0)
        {
            panel.Children.Add(Ui.Text("暂无公告，新的消息会出现在这里。"));
            return panel;
        }
        var entry = items[Math.Abs(index) % items.Length];
        panel.Children.Add(Ui.Text(entry.Title, 17));
        panel.Children.Add(Ui.Text(entry.Body.Length > 0 ? entry.Body : entry.Text));
        if (entry.ImageUrl.Length > 0)
            panel.Children.Add(new RemoteImage(Session, entry.ImageUrl));
        panel.Children.Add(Ui.Row(Ui.Text(entry.Author + " · " + entry.PublishedAt), Ui.Action("下一条", () => { index++; Draw(); })));
        return panel;
    }
    StackPanel Recommendations()
    {
        var panel = Ui.Stack(Ui.Row(Ui.Heading("群友推荐"), Ui.Action("浏览更多", () => Session.Navigate?.Invoke("community"))));
        foreach (var entry in Entries("recommendations").Take(3))
        {
            var button = Ui.Button(entry.Name, () => Recommendation(Session, Owner, entry));
            panel.Children.Add(Ui.Row(button, Ui.Text(entry.Description)));
        }
        if (panel.Children.Count == 1)
            panel.Children.Add(Ui.Text("暂无群友推荐。"));
        return panel;
    }
    StackPanel Quote()
    {
        var panel = Ui.Stack(Ui.Row(Ui.Text("“ 群友语录", 17), Ui.Action("下一条", () => { index++; Draw(); })));
        var entries = Entries("quotes");
        if (entries.Length == 0)
            panel.Children.Add(Ui.Text("还没有语录。"));
        else
        {
            var entry = entries[Math.Abs(index) % entries.Length];
            panel.Children.Add(Ui.Text(entry.Text + " —— " + entry.Author));
            if (entry.ImageUrl.Length > 0)
                panel.Children.Add(new RemoteImage(Session, entry.ImageUrl));
        }
        return panel;
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
