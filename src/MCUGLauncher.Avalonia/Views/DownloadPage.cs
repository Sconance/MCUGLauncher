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
    readonly global::Avalonia.Controls.Primitives.UniformGrid cards = new() { Columns = 1 };
    public DownloadPage(LauncherFacade session, string category) : base(session)
    {
        this.category = category;
        SizeChanged += (_, _) => cards.Columns = category == "vanilla" ? 1 : Math.Max(1, (int)(Bounds.Width / 270));
        query.Text = session.PendingSearch;
        session.PendingSearch = "";
        var label = category switch
        {
            "vanilla" => "原版游戏",
            "mod" => "模组",
            "modpack" => "整合包",
            "resourcepack" => "资源包",
            "shader" => "光影",
            "datapack" => "数据包",
            _ => "下载"
        };
        Title(label, "发现并下载 Minecraft " + label + "。");
        query.MinWidth = 0;
        Body.Children.Add(Ui.Beside(query, Ui.Button("搜索 / 刷新", Search, true)));
        if (category != "vanilla")
        {
            var filters = Ui.Row(Ui.Field("游戏版本", game));
            if (category is "mod" or "modpack")
                filters.Children.Add(Ui.Field("加载器", loader));
            Body.Children.Add(filters);
        }
        Body.Children.Add(results);
        if (category == "vanilla" || !string.IsNullOrWhiteSpace(query.Text))
            _ = Search();
    }

    async Task Search()
    {
        results.Children.Clear();
        results.Children.Add(Ui.Text("正在获取列表…"));
        try
        {
            var request = Guid.NewGuid().ToString("N");
            var response = category == "vanilla" ? await Session.RequestAsync("vanillaList", new
            {
                type = "vanillaVersions"
            }) : await Session.RequestAsync("contentList", new
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
            foreach (var item in response.Items.Where(i => category != "vanilla" || i.Id.Contains(query.Text ?? "", StringComparison.OrdinalIgnoreCase)))
            {
                var buttons = category == "vanilla" ? Ui.Row(
                    Ui.Button("安装", () => Send(new { type = "installVanilla", version = item.Id }), true),
                    Ui.Button("配置加载器", () => InstallDialogs.Loader(Session, Owner, item.Id))
                ) : Ui.Row(
                    Ui.Button("详情与下载", () => ResourceDialog.Open(Session, Owner, item, category), true),
                    Ui.Button("收藏 / 取消收藏", () => Send(new { type = "toggleBookmark", id = item.Id, name = item.Name, description = item.Description, author = item.Author, kind = category }))
                );
                var card = Ui.Card(Ui.Stack(
                            Ui.Row(
                                new RemoteImage(Session, item.Icon, 48),
                                Ui.Text(item.Title, 17)
                            ),
                            Ui.Text(category == "vanilla" ? item.Released : item.Description),
                            Ui.Text(item.Author + (category != "vanilla" ? $" · {item.Downloads:N0} 次下载" : "")),
                            buttons
                ));
                card.Margin = new global::Avalonia.Thickness(0, 0, 12, 12);
                cards.Children.Add(card);
            }
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
}
