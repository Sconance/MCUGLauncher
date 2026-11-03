using System.Text.Json;

namespace MCUGLauncher;

/// <summary>根据实例、会话与服务器请求准备启动参数，或启动并返回游戏会话；不依赖界面。</summary>
public static class ServerLaunch
{
    public static LaunchRequest Prepare(ServerLaunchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Root);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.InstanceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ServerAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OutputFile);
        ArgumentNullException.ThrowIfNull(request.Session);
        if (!request.Session.CheckIsValid())
            throw new ArgumentException("A valid Minecraft session is required.", nameof(request));
        var endpoint = ServerEndpoint.Parse(request.ServerAddress);
        var root = Path.GetFullPath(request.Root);
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException("The game root does not exist.");
        var catalog = new InstanceCatalog(root);
        var metadata = Path.Combine(catalog.MetadataDirectory(request.InstanceId), ".mcuglauncher-instance.json");
        var localProfile = Path.Combine(root, "versions", request.InstanceId, request.InstanceId + ".json");
        if (!File.Exists(metadata) && !File.Exists(localProfile))
            throw new FileNotFoundException("The requested instance is not installed.");
        var descriptor = catalog.GetOrCreate(request.InstanceId);
        var profile = descriptor.LaunchProfileId;
        if (!File.Exists(Path.Combine(root, "versions", profile, profile + ".json")) ||
            !VersionInstallation.IsReady(root, request.InstanceId) || !VersionInstallation.IsReady(root, profile))
            throw new IOException("The launch profile is missing or its installation is incomplete.");
        var settings = JsonSerializer.Deserialize<LaunchSettings>(JsonSerializer.Serialize(request.Settings ?? descriptor.Launch ?? new LaunchSettings()))!;
        settings.Server = endpoint.ToString();
        settings.Validate();
        var directory = settings.Isolation == "共享根目录" ? root : catalog.DirectoryFor(request.InstanceId);
        var memory = settings.AutoMemory ? MemoryPolicy.Recommend() : settings.RamMb;
        cancellationToken.ThrowIfCancellationRequested();
        return new(root, directory, profile, settings.Java, memory, settings.Width, settings.Height,
            request.Session, settings, request.ExternalAuthentication);
    }

    public static async Task<GameSession> LaunchAsync(
        ServerLaunchRequest request,
        CancellationToken cancellationToken = default,
        IProgress<string>? progress = null,
        Action<string>? output = null,
        HttpClient? http = null)
    {
        var launch = Prepare(request, cancellationToken);
        progress?.Report("Building game process.");
        var process = await LaunchService.Build(launch, cancellationToken, http);
        try
        {
            var secrets = request.Secrets.Append(request.Session.AccessToken ?? "").ToArray();
            var session = new GameSession(process, Path.GetFullPath(request.OutputFile), output ?? (_ => { }), secrets);
            progress?.Report("Starting game process.");
            cancellationToken.ThrowIfCancellationRequested();
            session.Start();
            return session;
        }
        catch
        {
            process.Dispose();
            throw;
        }
    }
}
