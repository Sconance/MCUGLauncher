namespace MCUGLauncher.Contracts;

/// <summary>A read-only application snapshot; no mutable configuration or UI objects escape.</summary>
public sealed record LauncherState
{
    public string AppVersion { get; init; } = "";
    public string Player { get; init; } = "";
    public string AccountType { get; init; } = "";
    public string SkinStation { get; init; } = "";
    public string Cape { get; init; } = "";
    public string WriteEndpoint { get; init; } = "";
    public VersionGroupInfo[] VersionGroups { get; init; } = [];
    public string Root { get; init; } = "";
    public string Java { get; init; } = "";
    public string SelectedVersion { get; init; } = "";
    public string SelectedInstance { get; init; } = "";
    public int Ram
    {
        get; init;
    }
    public int Width
    {
        get; init;
    }
    public int Height
    {
        get; init;
    }
    public bool AutoMemory
    {
        get; init;
    }
    public bool CommunityKey
    {
        get; init;
    }
    public bool AdminKey
    {
        get; init;
    }
    public bool Anonymous
    {
        get; init;
    }
    public string DataSourceMode { get; init; } = "";
    public InstanceInfo[] Instances { get; init; } = [];
    public ModInfo[] Mods { get; init; } = [];
    public ResourceInfo[] Favorites { get; init; } = [];
    public ServerInfo[] Servers { get; init; } = [];
    public string[] GameRoots { get; init; } = [];
    public DataSourceSet DataSources { get; init; } = new();
    public DataSourceSet DefaultDataSources { get; init; } = new();
    public DataSourceSet CustomDataSources { get; init; } = new();
    public PreferencesInfo Preferences { get; init; } = new();
}
