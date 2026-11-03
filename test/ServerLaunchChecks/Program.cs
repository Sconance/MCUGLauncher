using BlockLaunch;
using CmlLib.Core.Auth;

var root = Path.Combine(Path.GetTempPath(), "BlockLaunch-server-launch-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);

void Check(bool value, string name)
{
    if (!value)
        throw new Exception(name);
    Console.WriteLine("PASS " + name);
}

void Reject<TException>(Action action, string name) where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        Console.WriteLine("PASS " + name);
        return;
    }
    throw new Exception(name);
}

try
{
    var settings = new LaunchSettings
    {
        AutoMemory = false,
        RamMb = 2048,
        Server = "saved.example.com"
    };
    var request = new ServerLaunchRequest(root, "1.21.1", "play.example.com", MSession.CreateOfflineSession("Player"),
        Path.Combine(root, "logs", "game.log"), settings);
    Reject<FileNotFoundException>(() => ServerLaunch.Prepare(request), "missing instance rejected");
    Check(!Directory.Exists(Path.Combine(root, "instances")), "missing instance does not create metadata");
    Reject<IOException>(() => ServerLaunch.Prepare(request with { InstanceId = "../escape" }), "unsafe instance rejected");
    Reject<IOException>(() => ServerLaunch.Prepare(request with { ServerAddress = "host:70000" }), "invalid server port rejected");
    Reject<ArgumentException>(() => ServerLaunch.Prepare(request with { ServerAddress = "" }), "empty server rejected");
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();
    Reject<OperationCanceledException>(() => ServerLaunch.Prepare(request, cancellation.Token), "cancel before preparation");
    try
    {
        await ServerLaunch.LaunchAsync(request, cancellation.Token);
        throw new Exception("cancelled launch accepted");
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("PASS cancelled launch does not build or start a process");
    }

    var profileDirectory = Path.Combine(root, "versions", "1.21.1");
    Directory.CreateDirectory(profileDirectory);
    File.WriteAllText(Path.Combine(profileDirectory, "1.21.1.json"), "{}");
    var launch = ServerLaunch.Prepare(request);
    Check(launch.Profile == "1.21.1" && launch.Root == root, "requested game profile resolved");
    Check(launch.InstanceDirectory == Path.Combine(root, "instances", "1.21.1"), "isolated instance directory resolved");
    Check(launch.Settings!.Server == "play.example.com:25565", "default server port normalized");
    Check(launch.Ram == 2048 && ReferenceEquals(launch.Session, request.Session), "memory and session forwarded");
    Check(settings.Server == "saved.example.com" && !ReferenceEquals(settings, launch.Settings), "caller settings are not mutated");

    var catalog = new InstanceCatalog(root);
    var descriptor = catalog.GetOrCreate("custom-instance");
    descriptor.LaunchProfileId = "1.21.1";
    descriptor.DataDirectory = Path.Combine(root, "custom-data");
    descriptor.Launch = settings;
    catalog.Save(descriptor);
    var metadataFile = Path.Combine(catalog.MetadataDirectory(descriptor.Key), ".blocklaunch-instance.json");
    var originalMetadata = File.ReadAllText(metadataFile);
    var custom = ServerLaunch.Prepare(request with
    {
        InstanceId = descriptor.Key,
        Settings = null,
        ServerAddress = "[::1]:25566"
    });
    Check(custom.Profile == "1.21.1" && custom.InstanceDirectory == descriptor.DataDirectory, "instance alias and custom data directory resolved");
    Check(custom.Settings!.Server == "[::1]:25566" && custom.Ram == 2048, "instance settings and IPv6 server supported");
    Check(File.ReadAllText(metadataFile) == originalMetadata, "server override does not overwrite saved instance settings");
    var shared = ServerLaunch.Prepare(request with
    {
        Settings = new LaunchSettings
        {
            AutoMemory = false,
            Isolation = "共享根目录"
        }
    });
    Check(shared.InstanceDirectory == root, "shared root isolation supported");
    var preferences = new Preferences
    {
        Root = root,
        Launch = new LaunchSettings
        {
            AutoMemory = false,
            RamMb = 3072,
            Server = "global.example.com"
        }
    };
    var favorite = new ServerFavorite("favorite-id", "Favorite server", "favorite.example.com:25566", descriptor.Key);
    var external = new AuthlibLaunch("fixture.jar", "{}");
    var favoriteLaunch = new ServerLaunchFromFavourateList(favorite, preferences, request.Session, request.OutputFile, external)
    {
        Secrets = ["fixture-client-token"]
    };
    var favoriteRequest = favoriteLaunch.CreateRequest();
    Check(favoriteRequest.InstanceId == descriptor.Key && favoriteRequest.ServerAddress == favorite.Address,
        "favorite component maps its own instance and address");
    Check(ReferenceEquals(favoriteRequest.Session, request.Session) && favoriteRequest.OutputFile == request.OutputFile &&
        favoriteRequest.ExternalAuthentication == external && favoriteRequest.Secrets.SequenceEqual(favoriteLaunch.Secrets),
        "favorite component forwards authentication, logging and secrets");
    var favoritePrepared = ServerLaunch.Prepare(favoriteRequest);
    Check(favoritePrepared.Profile == "1.21.1" && favoritePrepared.Ram == 2048 &&
        favoritePrepared.Settings!.Server == favorite.Address, "favorite launch preserves instance settings and resolves profile");
    Check(File.ReadAllText(metadataFile) == originalMetadata && preferences.Launch.Server == "global.example.com",
        "favorite launch does not persist server overrides");
    descriptor.Launch = null;
    catalog.Save(descriptor);
    Check(ServerLaunch.Prepare(favoriteLaunch.CreateRequest()).Ram == 3072,
        "favorite component rereads instance settings and falls back to global settings");
    Reject<OperationCanceledException>(() => favoriteLaunch.CreateRequest(cancellation.Token), "favorite request supports cancellation");
    try
    {
        await favoriteLaunch.LaunchAsync(cancellation.Token);
        throw new Exception("cancelled favorite launch accepted");
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("PASS cancelled favorite launch does not start a process");
    }
    var missingFavorite = new ServerLaunchFromFavourateList(favorite with
    {
        Profile = "uninstalled"
    }, preferences, request.Session, request.OutputFile);
    Reject<FileNotFoundException>(() => ServerLaunch.Prepare(missingFavorite.CreateRequest()), "favorite with missing instance rejected");
    descriptor.LaunchProfileId = "missing-profile";
    catalog.Save(descriptor);
    Reject<IOException>(() => ServerLaunch.Prepare(request with { InstanceId = descriptor.Key }), "missing launch profile rejected");
    try
    {
        await VersionInstallation.Run(root, "1.21.1", _ => throw new IOException("fixture"), CancellationToken.None);
    }
    catch (IOException)
    {
    }
    Reject<IOException>(() => ServerLaunch.Prepare(request), "incomplete installation rejected");
}
finally
{
    Directory.Delete(root, true);
}
