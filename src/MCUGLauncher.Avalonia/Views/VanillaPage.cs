using Avalonia.Controls;
using MCUGLauncher.Avalonia.Presentation;

namespace MCUGLauncher.Avalonia.Views;

// Catalog presentation only. Manifest retrieval and installation remain existing use cases.
internal sealed class VanillaPage : NativePage
{
    const int PageSize = 50;
    readonly TextBox query = Ui.Input("", "搜索 Minecraft 版本");
    readonly StackPanel results = Ui.Stack();
    readonly Dictionary<string, Button> tabs = [];
    VanillaVersionInfo[] versions = [];
    string category = "release";
    int page;
    public VanillaPage(ILauncherService session, UiContext ui) : base(session, ui)
    {
        Title("原版游戏", "选择正式版、快照或远古版本；也可配置兼容的模组加载器。");
        query.MinWidth = 0;
        Body.Children.Add(Ui.Beside(query, Ui.Button("刷新列表", Load)));
        var categories = Ui.Row();
        foreach (var (label, kind) in new[] { ("正式版", "release"), ("快照版", "snapshot"), ("远古版", "old"), ("全部", "all") })
        {
            var tab = Ui.Action(label, () => { category = kind; page = 0; Draw(); });
            tabs[kind] = tab;
            categories.Children.Add(tab);
        }
        Body.Children.Add(categories);
        Body.Children.Add(results);
        query.TextChanged += (_, _) => { page = 0; Draw(); };
        _ = Load();
    }
    async Task Load()
    {
        results.Children.Clear();
        results.Children.Add(Ui.Text("正在读取版本清单…"));
        try
        {
            versions = (await Session.ListVanillaVersionsAsync()).Items;
            page = 0;
            Draw();
        }
        catch (Exception error)
        {
            results.Children.Clear();
            results.Children.Add(Ui.Text("读取失败：" + error.Message));
        }
    }
    void Draw()
    {
        foreach (var (kind, tab) in tabs)
            tab.Classes.Set("primary", kind == category);
        results.Children.Clear();
        var filtered = versions.Where(v => (category == "all" || (category == "old" ? v.Type is "old_alpha" or "old_beta" : v.Type == category))
            && v.Id.Contains(query.Text ?? "", StringComparison.OrdinalIgnoreCase)).ToArray();
        var pages = Math.Max(1, (filtered.Length + PageSize - 1) / PageSize);
        page = Math.Clamp(page, 0, pages - 1);
        foreach (var version in filtered.Skip(page * PageSize).Take(PageSize))
        {
            var actions = Ui.Row(Ui.Button("配置加载器", () => InstallDialogs.Loader(Session, UI, Owner, version.Id)),
                Ui.Button("安装", () => Session.InstallVanillaAsync(version: version.Id), true));
            results.Children.Add(Ui.Card(Ui.Beside(Ui.Stack(Ui.Text(version.Id, 14), Ui.Muted(version.Released)), actions)));
        }
        if (filtered.Length == 0)
            results.Children.Add(Ui.Text("没有匹配的版本。"));
        var previous = Ui.Action("上一页", () => { page--; Draw(); });
        var next = Ui.Action("下一页", () => { page++; Draw(); });
        previous.IsEnabled = page > 0;
        next.IsEnabled = page + 1 < pages;
        results.Children.Add(Ui.Row(previous, Ui.Text($"{page + 1} / {pages} 页 · {filtered.Length} 个版本"), next));
    }
}
