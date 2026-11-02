using MCUGLauncher.Avalonia.Views;
namespace MCUGLauncher.Avalonia.Presentation;
// Menu registration is data-driven; icons may be replaced with packaged image paths.
internal sealed record NavigationEntry(string Id, string Label, string Group, string Symbol, Func<LauncherFacade, NativePage> Create, string? IconPath = null);
internal static class NavigationCatalog
{
    public static IReadOnlyList<NavigationEntry> All
    {
        get;
    } = [
        new("home","启动","home","▷",s=>new HomePage(s)),
        new("vanilla","原版游戏","download","◇",s=>new DownloadPage(s,"vanilla")),
        new("mod","模组","download","⊞",s=>new DownloadPage(s,"mod")),
        new("modpack","整合包","download","▣",s=>new DownloadPage(s,"modpack")),
        new("resourcepack","资源包","download","▧",s=>new DownloadPage(s,"resourcepack")),
        new("shader","光影","download","☼",s=>new DownloadPage(s,"shader")),
        new("datapack","数据包","download","▤",s=>new DownloadPage(s,"datapack")),
        new("community","群友推荐与公告","download","♧",s=>new CommunityPage(s)),
        new("settings","启动与环境","settings","⚙",s=>new SettingsPage(s)),
        new("accounts","账号与皮肤","settings","♙",s=>new AccountsPage(s)),
        new("java","Java 管理","settings","♨",s=>new JavaPage(s)),
        new("sources","数据源","settings","▱",s=>new SourcesPage(s)),
        new("instances","实例与模组管理","settings","▣",s=>new InstancesPage(s)),
        new("servers","服务器收藏","settings","☷",s=>new ServersPage(s)),
        new("bookmarks","我的收藏","settings","♡",s=>new BookmarksPage(s)),
        new("saves","存档保护","settings","▤",s=>new SavesPage(s)),
        new("games","运行中的游戏","settings","▷",s=>new GamesPage(s)),
        new("tasks","下载任务","settings","↓",s=>new TasksPage(s)),
        new("editor","管理员与公告编辑","settings","♙",s=>new EditorPage(s)),
        new("diagnostics","诊断与关于","settings","ⓘ",s=>new DiagnosticsPage(s))
    ];
}
