using System.Diagnostics;
using Avalonia.Controls;
using MCUGLauncher.Avalonia.Presentation;

namespace MCUGLauncher.Avalonia.Views;
internal sealed class CommunityPage : NativePage
{
    readonly StackPanel entries = Ui.Stack();
    public CommunityPage(LauncherFacade session) : base(session)
    {
        Title("公告与群友推荐", "内容来自配置的数据源；没有内容或请求失败时不会填入演示数据。");
        Body.Children.Add(Ui.Row(
                Ui.Button("刷新社区内容", () => Send(new { type = "refreshCommunity" })),
                Ui.Action("服务器列表", () => Session.Navigate?.Invoke("servers"))
        ));
        Body.Children.Add(entries);
        Draw();
    }

    void Draw()
    {
        entries.Children.Clear();
        foreach (var key in new[]
            {
                "announcements",
                "recommendations",
                "quotes"
            }

        )
        {
            entries.Children.Add(Ui.Heading(key switch
            {
                "announcements" => "公告",
                "recommendations" => "群友推荐",
                _ => "群友语录"
            }));
            foreach (var entry in Session.Community.GetValueOrDefault(key) ?? [])
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
        var name = Ui.Input("", "名称");
        var address = Ui.Input("", "host:port");
        Body.Children.Add(Ui.Card(Ui.Stack(
                    Ui.Field("名称", name),
                    Ui.Field("地址", address),
                    Ui.Button("保存服务器", () => Send(new { type = "saveServer", name = name.Text ?? "", address = address.Text ?? "" }), true)
        )));
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
