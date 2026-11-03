using CmlLib.Core.Auth;

namespace BlockLaunch;

public sealed record ServerLaunchRequest(
    string Root,
    string InstanceId,
    string ServerAddress,
    MSession Session,
    string OutputFile,
    LaunchSettings? Settings = null,
    AuthlibLaunch? ExternalAuthentication = null)
{
    public IReadOnlyList<string> Secrets { get; init; } = [];
}
