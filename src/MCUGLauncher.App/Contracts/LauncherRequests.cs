namespace MCUGLauncher.Contracts;

public sealed record AppSettingsPatch
{
    public string? Root
    {
        get; init;
    }
    public string? Java
    {
        get; init;
    }
    public int? Ram
    {
        get; init;
    }
    public int? Width
    {
        get; init;
    }
    public int? Height
    {
        get; init;
    }
    public bool? AutoMemory
    {
        get; init;
    }
    public bool? Anonymous
    {
        get; init;
    }
    public string? Player
    {
        get; init;
    }
    public string? WriteEndpoint
    {
        get; init;
    }
    public DataSourceSet? DataSources
    {
        get; init;
    }
    public string[]? GameRoots
    {
        get; init;
    }
    public int? BackupCount
    {
        get; init;
    }
    public string? ResourceDownloadDirectory
    {
        get; init;
    }
    public NetworkSettingsInfo? Network
    {
        get; init;
    }
    public AppearancePatch? Appearance
    {
        get; init;
    }
}
