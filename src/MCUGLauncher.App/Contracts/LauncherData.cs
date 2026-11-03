namespace MCUGLauncher.Contracts;

// Immutable semantic snapshots shared by the application and its native clients.
public sealed record InstanceInfo
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Loader { get; init; } = "";
    public string Minecraft { get; init; } = "";
    public bool IsPlaceholder
    {
        get; init;
    }
    public string Title => DisplayName.Length > 0 ? DisplayName : Name.Length > 0 ? Name : Id;
    public override string ToString() => Title;
}

public sealed record ResourceInfo
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Description { get; init; } = "";
    public string Author { get; init; } = "";
    public string Icon { get; init; } = "";
    public long Downloads
    {
        get; init;
    }
    public string Kind { get; init; } = "";
    public string Title => DisplayName.Length > 0 ? DisplayName : Name.Length > 0 ? Name : Id;
    public override string ToString() => Title;
}

public sealed record ModInfo
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string File { get; init; } = "";
    public string Path { get; init; } = "";
    public string Version { get; init; } = "";
    public bool Enabled
    {
        get; init;
    }
    public long Size
    {
        get; init;
    }
}

public sealed record ServerInfo
{
    public string Name { get; init; } = "";
    public string Address { get; init; } = "";
}

public sealed record TaskInfo
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string State { get; init; } = "";
    public string Message { get; init; } = "";
    public double? Percent
    {
        get; init;
    }
    public bool Committing
    {
        get; init;
    }
}

public sealed record GameInfo
{
    public string Id { get; init; } = "";
    public string Version { get; init; } = "";
    public string Player { get; init; } = "";
}

public sealed record JavaInfo
{
    public string Path { get; init; } = "";
    public int Major
    {
        get; init;
    }
    public string Arch { get; init; } = "";
    public string Vendor { get; init; } = "";
    public string Error { get; init; } = "";
}

public sealed record SaveBackupInfo
{
    public string Id { get; init; } = "";
    public string Created { get; init; } = "";
    public string[] Worlds { get; init; } = [];
}

public sealed record ResourceVersionInfo
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string[] Files { get; init; } = [];
    public override string ToString() => Name;
}

public sealed record ProfileInfo
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public override string ToString() => Name;
}

public sealed record VanillaVersionInfo
{
    public string Id { get; init; } = "";
    public string Type { get; init; } = "";
    public string Released { get; init; } = "";
}

public sealed record ContentUpdateInfo
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Before { get; init; } = "";
    public string After { get; init; } = "";
    public bool Available
    {
        get; init;
    }
    public string Error { get; init; } = "";
}

public sealed record FileChangeInfo
{
    public string Path { get; init; } = "";
    public string Before { get; init; } = "";
    public string After { get; init; } = "";
}

public sealed record DataSourceSet
{
    public string Announcements { get; init; } = "";
    public string Servers { get; init; } = "";
    public string Recommendations { get; init; } = "";
    public string Quotes { get; init; } = "";
}

public sealed record NetworkSettingsInfo
{
    public string DownloadSource { get; init; } = "官方优先，失败切换 BMCLAPI";
    public string ProxyMode { get; init; } = "系统代理";
    public string ProxyAddress { get; init; } = "";
    public int LimitKibPerSecond
    {
        get; init;
    }
}

public sealed record AppearancePatch
{
    public string Path { get; init; } = "";
    public double Opacity
    {
        get; init;
    }
    public string Fit { get; init; } = "";
}

public sealed record PreferencesInfo
{
    public string BackgroundPath { get; init; } = "";
    public double BackgroundOpacity { get; init; } = 0.22;
    public string BackgroundFit { get; init; } = "裁剪填充";
    public string ResourceDownloadDirectory { get; init; } = "";
    public int SaveBackupCount { get; init; } = 5;
    public NetworkSettingsInfo Network { get; init; } = new();
}

public sealed record LaunchOptionsSettings
{
    public bool AutoChinese { get; init; } = true;
    public bool AutoMemory
    {
        get; init;
    }
    public bool FullScreen
    {
        get; init;
    }
    public string Java { get; init; } = "";
    public int RamMb { get; init; } = 4096;
    public int Width { get; init; } = 1280;
    public int Height { get; init; } = 720;
    public string JvmArguments { get; init; } = "";
    public string WindowTitle { get; init; } = "";
    public string Server { get; init; } = "";
}

public sealed record CommunityEntry
{
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
    public string Type { get; init; } = "";
    public string Addr { get; init; } = "";
    public string Version { get; init; } = "";
    public string Intro { get; init; } = "";
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Text { get; init; } = "";
    public string Body { get; init; } = "";
    public string Name { get; init; } = "";
    public string Author { get; init; } = "";
    public string ImageUrl { get; init; } = "";
    public string Url { get; init; } = "";
    public string Address { get; init; } = "";
    public string Description { get; init; } = "";
    public string PublishedAt { get; init; } = "";
    public string ExpiresAt { get; init; } = "";
}



public sealed record VersionGroupInfo(string Label, int Count, IReadOnlyList<string[]> Versions);
