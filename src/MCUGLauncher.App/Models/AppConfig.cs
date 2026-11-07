namespace MCUGLauncher.Models;

/// <summary>
/// 提供 App配置 相关功能。
/// </summary>
public sealed class AppConfig
{
    // Preserve the complete previous settings model, including currently hidden preferences.
    public BlockLaunch.Preferences LegacyPreferences { get; set; } = new();
    public double WindowWidth { get; set; } = 1200;
    public double WindowHeight { get; set; } = 780;
    public bool WindowMaximized
    {
        get; set;
    }
    /// <summary>
    /// 获取或设置 根目录。
    /// </summary>
    public string Root { get; set; } = "";
    public List<string> GameRoots { get; set; } = [];
    /// <summary>
    /// 获取或设置 Player。
    /// </summary>
    public string Player { get; set; } = "Steve";
    public string OfflinePlayer { get; set; } = "";
    /// <summary>
    /// 获取或设置 账号类型。
    /// </summary>
    public string AccountType { get; set; } = "offline";
    /// <summary>
    /// 获取或设置 Java。
    /// </summary>
    public string Java { get; set; } = "";
    /// <summary>
    /// 获取或设置 Ram。
    /// </summary>
    public int Ram { get; set; } = 4096;
    public bool AutoMemory
    {
        get; set;
    } = true;
    /// <summary>
    /// 获取或设置 Width。
    /// </summary>
    public int Width { get; set; } = 1280;
    /// <summary>
    /// 获取或设置 Height。
    /// </summary>
    public int Height { get; set; } = 720;
    /// <summary>
    /// 获取或设置 Last版本。
    /// </summary>
    public string LastVersion { get; set; } = "";
    /// <summary>
    /// 获取或设置 Last实例。
    /// </summary>
    public string LastInstance { get; set; } = "";
    public bool Anonymous
    {
        get; set;
    }
    /// <summary>
    /// 获取或设置 社区Key。
    /// </summary>
    public string CommunityKey { get; set; } = "";
    /// <summary>
    /// 获取或设置 AdminKey。
    /// </summary>
    public string AdminKey { get; set; } = "";
    /// <summary>
    /// 保留旧配置字段以兼容现有配置；公告写入功能已移除。
    /// </summary>
    public string WriteEndpoint { get; set; } = "";
    /// <summary>
    /// 获取或设置 数据Sources。
    /// </summary>
    public DataSourceConfig DataSources { get; set; } = new();
    // Saved custom addresses are independent of the addresses currently in use.
    public DataSourceConfig? CustomDataSources
    {
        get; set;
    }
    public string DataSourceMode { get; set; } = "";
    public string EffectiveDataSourceMode => DataSourceMode is "default" or "custom" ? DataSourceMode
        : System.Text.Json.JsonSerializer.Serialize(DataSources) == System.Text.Json.JsonSerializer.Serialize(new DataSourceConfig()) ? "default" : "custom";
    /// <summary>
    /// 获取或设置 Servers。
    /// </summary>
    public List<SavedServer> Servers { get; set; } = [];
    /// <summary>
    /// 获取或设置 皮肤文件。
    /// </summary>
    public string SkinFile { get; set; } = "";
    /// <summary>
    /// 获取或设置 Cape。
    /// </summary>
    public string Cape { get; set; } = "none";
    /// <summary>
    /// 获取或设置 皮肤Station。
    /// </summary>
    public string SkinStation { get; set; } = "";
}

/// <summary>
/// 提供 数据Source配置 相关功能。
/// </summary>
public sealed class DataSourceConfig
{
    /// <summary>
    /// 获取或设置 Announcements。
    /// </summary>
    public string Announcements { get; set; } = Services.AnnouncementSyncService.Primary;
    /// <summary>
    /// 获取或设置 Servers。
    /// </summary>
    public string Servers { get; set; } = "https://raw.githubusercontent.com/waibibabu1239/MCUGL-storage/main/servers.json";
    /// <summary>
    /// 获取或设置 Recommendations。
    /// </summary>
    public string Recommendations { get; set; } = "https://raw.githubusercontent.com/waibibabu1239/MCUGL-storage/main/recommendations.json";
    /// <summary>
    /// 获取或设置 Quotes。
    /// </summary>
    public string Quotes { get; set; } = "https://raw.githubusercontent.com/waibibabu1239/MCUGL-storage/main/quotes.json";
}

/// <summary>
/// 提供 Saved服务器 相关功能。
/// </summary>
public sealed class SavedServer
{
    /// <summary>启动此服务器的实例标识；空字符串表示未绑定。</summary>
    public string Instance { get; set; } = "";
    /// <summary>
    /// 获取或设置 名称。
    /// </summary>
    public string Name { get; set; } = "";
    /// <summary>
    /// 获取或设置 地址。
    /// </summary>
    public string Address { get; set; } = "";
}

/// <summary>
/// 表示 版本分组 数据。
/// </summary>
public sealed record VersionGroup(string Label, int Count, IReadOnlyList<string[]> Versions);

/// <summary>
/// 表示 实例信息 数据。
/// </summary>
public sealed record InstanceInfo(string Name, string Loader, int Mods, int Updates)
{
    public string DisplayName { get; init; } = Name;
    public string Minecraft { get; init; } = "";
    public bool IsPlaceholder
    {
        get; init;
    }
}

/// <summary>
/// 表示 模组信息 数据。
/// </summary>
public sealed record ModInfo(
    string Id,
    string Name,
    string File,
    string Version,
    bool Enabled,
    string Path,
    long Size,
    string Loader);

/// <summary>
/// 表示 启动结果 数据。
/// </summary>
public sealed record LaunchResult(bool Ok, string Message);
