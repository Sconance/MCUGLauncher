namespace MCUGLauncher.Contracts;

public sealed record OperationReceipt
{
    public string Token { get; init; } = "";
    public bool Ok
    {
        get; init;
    }
    public string Message { get; init; } = "";
}

public sealed record AuthResultInfo
{
    public bool Ok
    {
        get; init;
    }
    public bool Pending
    {
        get; init;
    }
    public string Message { get; init; } = "";
}

public sealed record ServerResultInfo
{
    public bool Ok
    {
        get; init;
    }
    public string Message { get; init; } = "";
}

public sealed record ServerPingInfo
{
    public string Id { get; init; } = "";
    public bool Online
    {
        get; init;
    }
    public int Players
    {
        get; init;
    }
    public int Max
    {
        get; init;
    }
    public int Ping
    {
        get; init;
    }
    public string Error { get; init; } = "";
}

public sealed record TestResultInfo
{
    public string Name { get; init; } = "";
    public string Url { get; init; } = "";
    public string RequestId { get; init; } = "";
    public bool Ok
    {
        get; init;
    }
    public int Ms
    {
        get; init;
    }
    public string Message { get; init; } = "";
}

public sealed record KeyResultInfo
{
    public bool Ok
    {
        get; init;
    }
    public string Message { get; init; } = "";
}

public sealed record AccountStateInfo
{
    public string AvatarIdentity { get; init; } = "";
    public string Player { get; init; } = "";
    public string AccountType { get; init; } = "";
}

public sealed record AccountSwitchResultInfo
{
    public string Account { get; init; } = "";
    public bool NeedsLogin
    {
        get; init;
    }
    public bool Ok
    {
        get; init;
    }
    public string Message { get; init; } = "";
}

public sealed record AvatarInfo
{
    public string AvatarIdentity { get; init; } = "";
    public string Account { get; init; } = "";
    public string Player { get; init; } = "";
    public string Image { get; init; } = "";
    public bool Fallback
    {
        get; init;
    }
}

public sealed record MuaProfilesInfo
{
    public string Token { get; init; } = "";
    public ProfileInfo[] Items { get; init; } = [];
}

public sealed record ContentListInfo
{
    public string RequestId { get; init; } = "";
    public ResourceInfo[] Items { get; init; } = [];
}

public sealed record ResourceVersionsInfo
{
    public string Token { get; init; } = "";
    public string RequestId { get; init; } = "";
    public string Game { get; init; } = "";
    public string Loader { get; init; } = "";
    public ResourceVersionInfo[] Items { get; init; } = [];
}

public sealed record LoaderListInfo
{
    public string RequestId { get; init; } = "";
    public string Version { get; init; } = "";
    public string Loader { get; init; } = "";
    public string[] Versions { get; init; } = [];
}

public sealed record VanillaListInfo
{
    public VanillaVersionInfo[] Items { get; init; } = [];
}

public sealed record JavaListInfo
{
    public JavaInfo[] Items { get; init; } = [];
}

public sealed record PackPreviewInfo
{
    public string Token { get; init; } = "";
    public string Name { get; init; } = "";
    public string Root { get; init; } = "";
    public string Game { get; init; } = "";
    public string Loader { get; init; } = "";
}

public sealed record LocalImportPreviewInfo
{
    public string[] Choices { get; init; } = [];
    public bool CanImport { get; init; } = true;
    public string[] Warnings { get; init; } = [];
    public string Token { get; init; } = "";
    public string Kind { get; init; } = "";
    public string Path { get; init; } = "";
    public string Name { get; init; } = "";
    public string Target { get; init; } = "";
}

public sealed record LaunchOptionsInfo
{
    public string Token { get; init; } = "";
    public string Name { get; init; } = "";
    public bool Inherited
    {
        get; init;
    }
    public LaunchOptionsSettings Settings { get; init; } = new();
}

public sealed record UninstallPreviewInfo
{
    public string Token { get; init; } = "";
    public string Notice { get; init; } = "";
    public string[] Paths { get; init; } = [];
}

public sealed record ContentUpdatesInfo
{
    public string Token { get; init; } = "";
    public ContentUpdateInfo[] Items { get; init; } = [];
}

public sealed record ContentUpdatePreviewInfo
{
    public string Token { get; init; } = "";
    public FileChangeInfo[] Changes { get; init; } = [];
}

public sealed record LogTextInfo
{
    public string Text { get; init; } = "";
}

public sealed record SystemInfoInfo
{
    public string App { get; init; } = "";
    public string Dotnet { get; init; } = "";
    public string Os { get; init; } = "";
    public string Root { get; init; } = "";
    public string Data { get; init; } = "";
}
