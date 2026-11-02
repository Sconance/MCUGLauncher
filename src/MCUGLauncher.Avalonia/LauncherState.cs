using MCUGLauncher.Models;
namespace MCUGLauncher.Avalonia;
using Presentation;
internal sealed record LauncherState
{
    public string AppVersion { get; init; } = BlockLaunch.ProductInfo.Version;
    public string Player { get; init; } = "";
    public string AccountType { get; init; } = "offline";
    public string AccountLabel { get; init; } = "离线账号";
    public string Root { get; init; } = "";
    public string Java { get; init; } = "";
    public string SelectedVersion { get; init; } = "";
    public string SelectedInstance { get; init; } = "";
    public int Ram { get; init; } = 4096;
    public int Width { get; init; } = 1280;
    public int Height { get; init; } = 720;
    public bool AutoMemory
    {
        get; init;
    }
    public bool CommunityKey
    {
        get; init;
    }
    public bool Anonymous
    {
        get; init;
    }
    public string DataSourceMode { get; init; } = "default";
    public NativeItem[] Instances { get; init; } = [];
    public NativeItem[] Mods { get; init; } = [];
    public NativeItem[] Favorites { get; init; } = [];
    public NativeItem[] Servers { get; init; } = [];
    public string[] GameRoots { get; init; } = [];
    public DataSourceConfig DataSources { get; init; } = new();
    public DataSourceConfig DefaultDataSources { get; init; } = new();
    public DataSourceConfig CustomDataSources { get; init; } = new();
}
