namespace MCUGLauncher.Contracts;

/// <summary>请求启动指定实例并直连服务器；不携带账号凭据或界面状态。</summary>
public sealed record ServerLaunchInfo(string InstanceId, string ServerAddress);
