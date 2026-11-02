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
    public DownloadPage(LauncherFacade session, string category) : base(session)
    {
        this.category = category;
        query.Text = session.PendingSearch;
        session.PendingSearch = "";
        Title(category == "vanilla" ? "原版游戏与加载器" : "资源下载", "数据来自 Mojang / Modrinth，下载交给现有 Core 队列校验和安装。");
        var filters = Ui.Row(
            query,
            game
        );
        if (category is "mod" or "modpack")
            filters.Children.Add(loader);
        filters.Children.Add(Ui.Button("搜索 / 刷新", Search, true));
        Body.Children.Add(Ui.Card(filters));
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
            foreach (var item in response.Items.Where(i => category != "vanilla" || i.Id.Contains(query.Text ?? "", StringComparison.OrdinalIgnoreCase)))
            {
                var buttons = category == "vanilla" ? Ui.Row(
                    Ui.Button("安装", () => Send(new { type = "installVanilla", version = item.Id }), true),
                    Ui.Button("配置加载器", () => InstallDialogs.Loader(Session, Owner, item.Id))
                ) : Ui.Row(
                    Ui.Button("详情与下载", () => ResourceDialog.Open(Session, Owner, item, category), true),
                    Ui.Button("收藏 / 取消收藏", () => Send(new { type = "toggleBookmark", id = item.Id, name = item.Name, description = item.Description, author = item.Author, kind = category }))
                );
                results.Children.Add(Ui.Card(Ui.Stack(
                            Ui.Row(
                                new RemoteImage(Session, item.Icon, 48),
                                Ui.Text(item.Title, 17)
                            ),
                            Ui.Text(category == "vanilla" ? item.Released : item.Description),
                            Ui.Text(item.Author + (category != "vanilla" ? $" · {item.Downloads:N0} 次下载" : "")),
                            buttons
                )));
            }

            if (results.Children.Count == 0)
                results.Children.Add(Ui.Text("没有匹配的结果，请调整条件。"));
        }
        catch (Exception ex)
        {
            results.Children.Clear();
            results.Children.Add(Ui.Text("获取失败：" + ex.Message));
        }
    }
}
