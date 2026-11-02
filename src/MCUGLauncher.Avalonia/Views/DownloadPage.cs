using Avalonia.Controls;
using MCUGLauncher.Avalonia.Presentation;

namespace MCUGLauncher.Avalonia.Views;
internal sealed class DownloadPage : NativePage
{
    readonly string category;
    readonly TextBox query = Ui.Input("", "搜索名称、作者或说明");
    readonly TextBox game = Ui.Input("", "游戏版本（留空不限）");
    readonly ComboBox loader = Ui.Select(new[] { "", "fabric", "forge", "neoforge", "quilt" });
    readonly StackPanel results = Ui.Stack();
    readonly Dictionary<string, Button> bookmarkButtons = [];
    readonly global::Avalonia.Controls.Primitives.UniformGrid cards = new() { Columns = 1 };
    public DownloadPage(LauncherFacade session, string category) : base(session)
    {
        this.category = category;
        SizeChanged += (_, _) => cards.Columns = Math.Max(1, (int)(Bounds.Width / 270));
        query.Text = session.PendingSearch;
        session.PendingSearch = "";
        var label = category switch
        {
            "mod" => "模组",
            "modpack" => "整合包",
            "resourcepack" => "资源包",
            "shader" => "光影",
            "datapack" => "数据包",
            _ => "下载"
        };
        Title(label, "发现并下载 Minecraft " + label + "。");
        query.MinWidth = 0;
        Body.Children.Add(Ui.Beside(query, Ui.Button("搜索", Search, true)));
        if (category is "mod" or "modpack" or "resourcepack" or "shader" or "datapack")
        {
            var filters = Ui.Row(Ui.Field("游戏版本", game));
            if (category is "mod" or "modpack")
                filters.Children.Add(Ui.Field("加载器", loader));
            Body.Children.Add(filters);
        }
        Body.Children.Add(results);
        if (!string.IsNullOrWhiteSpace(query.Text))
            _ = Search();
    }

    async Task Search()
    {
        results.Children.Clear();
        results.Children.Add(Ui.Text("正在获取列表…"));
        try
        {
            var request = Guid.NewGuid().ToString("N");
            var response = await Session.RequestAsync("contentList", new
            {
                type = "searchContent",
                category,
                query = query.Text ?? "",
                game = game.Text ?? "",
                loader = category is "mod" or "modpack" ? loader.SelectedItem as string ?? "" : "",
                requestId = request
            }, m => m.RequestId == request);
            results.Children.Clear();
            cards.Children.Clear();
            bookmarkButtons.Clear();
            foreach (var item in response.Items)
            {
                var bookmark = Ui.Button("♡", () => Send(new { type = "toggleBookmark", id = item.Id, name = item.Name, description = item.Description, author = item.Author, kind = category }));
                bookmark.Classes.Add("bookmark");
                bookmarkButtons[item.Id] = bookmark;
                var buttons = Ui.Row(
                    Ui.Button(category == "modpack" ? "下载整合包" : "下载", () => ResourceDialog.Open(Session, Owner, item, category), true),
                    bookmark
                );
                var card = Ui.Card(Ui.Stack(
                            Ui.Row(
                                new RemoteImage(Session, item.Icon, 48),
                                Ui.Text(item.Title, 17)
                            ),
                            Ui.Text(item.Description),
                            Ui.Text(item.Author + $" · {item.Downloads:N0} 次下载"),
                            buttons
                ));
                card.Margin = new global::Avalonia.Thickness(0, 0, 12, 12);
                cards.Children.Add(card);
            }
            RefreshBookmarks();
            if (cards.Children.Count == 0)
                results.Children.Add(Ui.Text("没有匹配的结果，请调整条件。"));
            else
                results.Children.Add(cards);
        }
        catch (Exception ex)
        {
            results.Children.Clear();
            results.Children.Add(Ui.Text("获取失败：" + ex.Message));
        }
    }
    void RefreshBookmarks()
    {
        foreach (var (id, button) in bookmarkButtons)
        {
            var active = State.Favorites.Any(f => f.Id == id && f.Kind == category);
            button.Content = active ? "♥" : "♡";
            ToolTip.SetTip(button, active ? "取消收藏" : "添加收藏");
            global::Avalonia.Automation.AutomationProperties.SetName(button, active ? "取消收藏" : "添加收藏");
        }
    }
    protected override void Receive(NativeMessage message)
    {
        if (message.Type == "state")
            RefreshBookmarks();
    }
}
