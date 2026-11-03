using MCUGLauncher.Avalonia.Views;
namespace MCUGLauncher.Avalonia.Presentation;
internal sealed record NavigationEntry(string Id, string Label, string Group, string Symbol, Func<ILauncherService, UiContext, NativePage> Create, string? IconPath = null, string Section = "", bool Visible = true);
internal static class NavigationCatalog
{
    public static IReadOnlyList<NavigationEntry> All
    {
        get;
    } = [
        new("home","启动","home","▷",(s,u)=>new HomePage(s,u)),
        new("community","群友推荐","download","♧",(s,u)=>new CommunityPage(s,u,true),Section:"社区"),
        new("vanilla","原版游戏","download","◇",(s,u)=>new VanillaPage(s,u),Section:"下载"),
        new("mod","模组","download","⊞",(s,u)=>new DownloadPage(s,u,"mod"),Section:"下载"),
        new("modpack","整合包","download","▣",(s,u)=>new DownloadPage(s,u,"modpack"),Section:"下载"),
        new("resourcepack","资源包","download","▧",(s,u)=>new DownloadPage(s,u,"resourcepack"),Section:"下载"),
        new("shader","光影","download","☼",(s,u)=>new DownloadPage(s,u,"shader"),Section:"下载"),
        new("datapack","数据包","download","▤",(s,u)=>new DownloadPage(s,u,"datapack"),Section:"下载"),
        new("download-install","安装与导入","download","▣",(s,u)=>new UtilitySettingsPage(s,u,"install"),Section:"下载"),
        new("settings","启动设置","settings","▷",(s,u)=>new SettingsPage(s,u),Section:"游戏"),
        new("java","Java 管理","settings","♨",(s,u)=>new JavaPage(s,u),Section:"游戏"),
        new("directories","游戏目录","settings","▱",(s,u)=>new DirectoriesPage(s,u),Section:"游戏"),
        new("instances","实例维护","settings","▣",(s,u)=>new InstancesPage(s,u),Section:"游戏"),
        new("saves","存档保护","settings","▤",(s,u)=>new SavesPage(s,u),Section:"游戏"),
        new("appearance","界面与背景","settings","▧",(s,u)=>new AppearancePage(s,u),Section:"启动器"),
        new("network","下载与网络","settings","↓",(s,u)=>new NetworkSettingsPage(s,u),Section:"启动器"),
        new("install","安装与导入","settings","▣",(s,u)=>new UtilitySettingsPage(s,u,"install"),Section:"启动器"),
        new("recovery","维护与恢复","settings","↻",(s,u)=>new UtilitySettingsPage(s,u,"recovery"),Section:"启动器"),
        new("options","账号与社区","settings","♙",(s,u)=>new CommunityOptionsPage(s,u),Section:"账号与社区"),
        new("sources","数据源","settings","▱",(s,u)=>new SourcesPage(s,u),Section:"账号与社区"),
        new("servers","服务器收藏","settings","☷",(s,u)=>new ServersPage(s,u),Section:"账号与社区"),
        new("bookmarks","我的收藏","settings","♡",(s,u)=>new BookmarksPage(s,u),Section:"账号与社区"),
        new("editor","管理员","settings","♙",(s,u)=>new EditorPage(s,u),Section:"账号与社区"),
        new("tasks","下载任务","settings","↓",(s,u)=>new TasksPage(s,u),Section:"任务与诊断"),
        new("games","运行中的游戏","settings","▷",(s,u)=>new GamesPage(s,u),Section:"任务与诊断"),
        new("logs","运行日志","settings","▤",(s,u)=>new UtilitySettingsPage(s,u,"logs"),Section:"任务与诊断"),
        new("diagnose","诊断与反馈","settings","ⓘ",(s,u)=>new UtilitySettingsPage(s,u,"diagnose"),Section:"任务与诊断"),
        new("about","关于","settings","ⓘ",(s,u)=>new UtilitySettingsPage(s,u,"about"),Section:"关于"),
        new("manage","版本内容管理","home","⚙",(s,u)=>new InstancesPage(s,u,true),Visible:false),
        new("version","版本设置","home","⚙",(s,u)=>new VersionOverviewPage(s,u),Visible:false)
    ];
}
