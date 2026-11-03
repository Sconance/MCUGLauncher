using CmlLib.Core.Auth;

namespace MCUGLauncher;

/// <summary>携带服务器直连所需的实例标识、会话、日志路径及可选启动设置。</summary>
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
