namespace MCUGLauncher.Contracts;

public sealed record AppSettingsPatch(
    string? Root = null,
    string? Java = null,
    int? Ram = null,
    int? Width = null,
    int? Height = null,
    bool? AutoMemory = null,
    bool? Anonymous = null,
    string? Player = null,
    string? WriteEndpoint = null,
    DataSourceSet? DataSources = null);

public sealed record LaunchOptionsSettings(
    bool AutoMemory,
    bool FullScreen,
    int RamMb,
    int Width,
    int Height,
    string Java,
    string JvmArguments,
    string WindowTitle,
    string Server);
