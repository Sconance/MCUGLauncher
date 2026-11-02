using MCUGLauncher.Avalonia.Views;
namespace MCUGLauncher.Avalonia.Presentation;
internal sealed record NavigationEntry(string Id, string Label, string Group, string Symbol, Func<LauncherFacade, NativePage> Create, string? IconPath = null, string Section = "", bool Visible = true);
internal static class NavigationCatalog
{
    public static IReadOnlyList<NavigationEntry> All
    {
        get;
    } = [
        new("home","启动","home","▷",s=>new HomePage(s)),
        new("community","群友推荐","download","♧",s=>new CommunityPage(s,true),Section:"社区"),
        new("vanilla","原版游戏","download","◇",s=>new VanillaPage(s),Section:"下载"),
        new("mod","模组","download","⊞",s=>new DownloadPage(s,"mod"),Section:"下载"),
        new("modpack","整合包","download","▣",s=>new DownloadPage(s,"modpack"),Section:"下载"),
        new("resourcepack","资源包","download","▧",s=>new DownloadPage(s,"resourcepack"),Section:"下载"),
        new("shader","光影","download","☼",s=>new DownloadPage(s,"shader"),Section:"下载"),
        new("datapack","数据包","download","▤",s=>new DownloadPage(s,"datapack"),Section:"下载"),
        new("download-install","安装与导入","download","▣",s=>new UtilitySettingsPage(s,"install"),Section:"下载"),
        new("settings","启动设置","settings","▷",s=>new SettingsPage(s),Section:"游戏"),
        new("java","Java 管理","settings","♨",s=>new JavaPage(s),Section:"游戏"),
        new("directories","游戏目录","settings","▱",s=>new DirectoriesPage(s),Section:"游戏"),
        new("instances","实例维护","settings","▣",s=>new InstancesPage(s),Section:"游戏"),
        new("saves","存档保护","settings","▤",s=>new SavesPage(s),Section:"游戏"),
        new("appearance","界面与背景","settings","▧",s=>new AppearancePage(s),Section:"启动器"),
        new("network","下载与网络","settings","↓",s=>new NetworkSettingsPage(s),Section:"启动器"),
        new("install","安装与导入","settings","▣",s=>new UtilitySettingsPage(s,"install"),Section:"启动器"),
        new("recovery","维护与恢复","settings","↻",s=>new UtilitySettingsPage(s,"recovery"),Section:"启动器"),
        new("options","账号与社区","settings","♙",s=>new CommunityOptionsPage(s),Section:"账号与社区"),
        new("sources","数据源","settings","▱",s=>new SourcesPage(s),Section:"账号与社区"),
        new("servers","服务器收藏","settings","☷",s=>new ServersPage(s),Section:"账号与社区"),
        new("bookmarks","我的收藏","settings","♡",s=>new BookmarksPage(s),Section:"账号与社区"),
        new("editor","管理员","settings","♙",s=>new EditorPage(s),Section:"账号与社区"),
        new("tasks","下载任务","settings","↓",s=>new TasksPage(s),Section:"任务与诊断"),
        new("games","运行中的游戏","settings","▷",s=>new GamesPage(s),Section:"任务与诊断"),
        new("logs","运行日志","settings","▤",s=>new UtilitySettingsPage(s,"logs"),Section:"任务与诊断"),
        new("diagnose","诊断与反馈","settings","ⓘ",s=>new UtilitySettingsPage(s,"diagnose"),Section:"任务与诊断"),
        new("about","关于","settings","ⓘ",s=>new UtilitySettingsPage(s,"about"),Section:"关于"),
        new("manage","版本内容管理","home","⚙",s=>new InstancesPage(s,true),Visible:false),
        new("version","版本设置","home","⚙",s=>new VersionOverviewPage(s),Visible:false)
    ];
}
