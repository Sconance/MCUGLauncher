using CmlLib.Core.Auth;

namespace BlockLaunch;

public sealed class ServerLaunchFromFavourateList(
    ServerFavorite favorite,
    Preferences preferences,
    MSession session,
    string outputFile,
    AuthlibLaunch? externalAuthentication = null)
{
    public IReadOnlyList<string> Secrets { get; init; } = [];

    public ServerLaunchRequest CreateRequest(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(favorite);
        ArgumentNullException.ThrowIfNull(preferences);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(preferences.Root);
        ArgumentException.ThrowIfNullOrWhiteSpace(favorite.Profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(favorite.Address);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFile);
        var root = Path.GetFullPath(preferences.Root);
        var catalog = new InstanceCatalog(root);
        var metadata = Path.Combine(catalog.MetadataDirectory(favorite.Profile), ".blocklaunch-instance.json");
        var settings = File.Exists(metadata) ? AtomicFile.ReadJson<InstanceDescriptor>(metadata).Launch : null;
        cancellationToken.ThrowIfCancellationRequested();
        return new(root, favorite.Profile, favorite.Address, session, outputFile,
            settings ?? preferences.Launch, externalAuthentication)
        {
            Secrets = Secrets.ToArray()
        };
    }

    public Task<GameSession> LaunchAsync(
        CancellationToken cancellationToken = default,
        IProgress<string>? progress = null,
        Action<string>? output = null,
        HttpClient? http = null)
    {
        return ServerLaunch.LaunchAsync(CreateRequest(cancellationToken), cancellationToken, progress, output, http);
    }
}
