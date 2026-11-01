namespace MCUGLauncher.Models;

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
    public string Root { get; set; } = "";
    public List<string> GameRoots { get; set; } = [];
    public string Player { get; set; } = "Steve";
    public string OfflinePlayer { get; set; } = "";
    public string AccountType { get; set; } = "offline";
    public string Java { get; set; } = "";
    public int Ram { get; set; } = 4096;
    public int Width { get; set; } = 1280;
    public int Height { get; set; } = 720;
    public string LastVersion { get; set; } = "";
    public string LastInstance { get; set; } = "";
    public bool Anonymous
    {
        get; set;
    }
    public string CommunityKey { get; set; } = "";
    public string AdminKey { get; set; } = "";
    public string WriteEndpoint { get; set; } = "";
    public DataSourceConfig DataSources { get; set; } = new();
    public List<SavedServer> Servers { get; set; } = [];
    public string SkinFile { get; set; } = "";
    public string Cape { get; set; } = "none";
    public string SkinStation { get; set; } = "";
}

public sealed class DataSourceConfig
{
    public string Announcements { get; set; } = "https://raw.githubusercontent.com/waibibabu1239/MCUGL-storage/main/announcements.json";
    public string Servers { get; set; } = "https://raw.githubusercontent.com/waibibabu1239/MCUGL-storage/main/servers.json";
    public string Recommendations { get; set; } = "https://raw.githubusercontent.com/waibibabu1239/MCUGL-storage/main/recommendations.json";
    public string Quotes { get; set; } = "https://raw.githubusercontent.com/waibibabu1239/MCUGL-storage/main/quotes.json";
}

public sealed class SavedServer
{
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
}

public sealed record VersionGroup(string Label, int Count, IReadOnlyList<string[]> Versions);

public sealed record InstanceInfo(string Name, string Loader, int Mods, int Updates)
{
    public string DisplayName { get; init; } = Name;
    public string Minecraft { get; init; } = "";
}

public sealed record ModInfo(
    string Id,
    string Name,
    string File,
    string Version,
    bool Enabled,
    string Path,
    long Size,
    string Loader);

public sealed record LaunchResult(bool Ok, string Message);
