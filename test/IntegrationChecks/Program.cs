using MCUGLauncher;
using CmlLib.Core.Auth;
using System.Text.Json;

var root = Path.GetFullPath("work/test-minecraft");
var catalog = new InstanceCatalog(root);
catalog.MigrateLegacy();
if (args.Contains("--mua-agent"))
{
    using var live = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(40)
    };
    var jar = await AuthlibLaunch.Prepare(Path.GetFullPath("work/v0170-agent 中文 cache"), live, default);
    var metadata = await live.GetStringAsync(AuthlibLaunch.Api);
    foreach (var minecraft in new[] {
        "1.21.1",
        "1.12.2"
    }

    )
    {
        var key = catalog.Create("MUA synthetic integration " + minecraft, minecraft, minecraft);
        await Run(key, "MUA_AGENT_" + minecraft, minecraft, 2048, new AuthlibLaunch(jar, metadata));
    }

    Console.WriteLine("MUA_AGENT_REAL_GAME_RENDER_PASSED (synthetic session, no real MUA login or server join)");
    return;
}

if (args.Contains("--server-arguments"))
{
    foreach (var address in new[] {
        "example.com:25566",
        "[::1]:25565",
        "[::1]:25566"
    }

    )
    {
        var settings = new LaunchSettings
        {
            Server = address
        };
        var key = catalog.Create("0.16 server arguments", "1.21.1", "1.21.1");
        var process = await LaunchService.Build(new(root, catalog.DirectoryFor(key), "1.21.1", "", 2048, 854, 480, MSession.CreateOfflineSession("Fixture"), settings), CancellationToken.None);
        var command = process.StartInfo.Arguments + " " + string.Join(" ", process.StartInfo.ArgumentList);
        var endpoint = ServerEndpoint.Parse(address);
        if (!command.Contains(endpoint.Host, StringComparison.Ordinal) || endpoint.Port != 25565 && !command.Contains(endpoint.Port.ToString(), StringComparison.Ordinal) || endpoint.Host.Contains(':') && !command.Contains("[" + endpoint.Host + "]", StringComparison.Ordinal))
            throw new Exception("Server connection arguments not emitted for " + address);
        Console.WriteLine("SERVER_ARGUMENTS_OK " + address);
    }

    return;
}

if (args.Contains("--large-pack"))
{
    using var live = NetworkPolicyHandler.Create(new());
    var archive = Path.GetFullPath("work/v015-large-pack/Create Live 5-1.3.2.1.mrpack");
    var largePack = await new PackService(live).Import(archive, root, r => new GameInstaller(live, root, "", Console.WriteLine).Install(r), false, Console.WriteLine);
    catalog.GetOrCreate(largePack.InstanceId);
    var mods = Directory.GetFiles(Path.Combine(catalog.DirectoryFor(largePack.InstanceId), "mods"), "*.jar").Length;
    Console.WriteLine("LARGE_PACK_MOD_COUNT=" + mods);
    await Run(largePack.InstanceId, "LARGE_CREATE_LIVE_5", largePack.Minecraft, 5120);
    Console.WriteLine("LARGE_PACK_INSTALL_AND_LAUNCH_PASSED " + largePack.InstanceId);
    return;
}

if (args.Contains("--extended"))
{
    using var live = NetworkPolicyHandler.Create(new());
    foreach (var mc in new[] {
        "1.16.5",
        "1.20.1"
    }

    )
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(12));
        var version = (await LoaderCatalog.Get(live, mc, "forge", deadline.Token)).First(r => !r.Version.Contains("beta")).Version;
        var profile = await new GameInstaller(live, root, "", Console.WriteLine, deadline.Token).Install(new(mc, "forge", version));
        var key = catalog.Create("0.15 Forge " + mc, profile, mc);
        await Run(key, "EXTENDED_FORGE_" + mc, mc);
    }

    Console.WriteLine("EXTENDED_FORGE_COMPATIBILITY_PASSED");
    return;
}

if (args.Contains("--real-mods"))
{
    using var live = NetworkPolicyHandler.Create(new());
    var service = new ContentService(live);
    var versions = (await service.Versions("sodium", "1.21.1", "fabric", "mod")).Where(v => v.VersionType == "release").ToList();
    if (versions.Count < 2)
        throw new Exception("Need two compatible real Sodium versions");
    var key = catalog.Create("0.15 real mod update rollback", "fabric-loader-0.19.5-1.21.1", "1.21.1");
    var directory = catalog.DirectoryFor(key);
    await service.Install(await service.Resolve(versions[1], "1.21.1", "fabric"), directory, Console.WriteLine);
    var preview = await ContentUpdates.Preview(service, directory, [versions[0]], "1.21.1", "fabric", default);
    await service.Install(preview.Plan, directory, Console.WriteLine, preview.Fingerprint);
    await Run(key, "REAL_MOD_UPDATED");
    ContentTransaction.RestoreLatest(directory);
    await Run(key, "REAL_MOD_ROLLED_BACK");
    Console.WriteLine("REAL_MOD_UPDATE_AND_ROLLBACK_PASSED " + directory);
    return;
}

if (args.Contains("--real-pack-upgrade"))
{
    using var live = NetworkPolicyHandler.Create(new());
    var service = new ContentService(live);
    var packs = new PackService(live);
    var versions = (await service.Versions("fabulously-optimized", "1.21.1", "fabric", "modpack")).Where(v => v.VersionType == "release").Take(2).ToList();
    if (versions.Count < 2)
        throw new Exception("Need two real pack releases");
    async Task<string> Archive(ContentVersion v)
    {
        var file = v.Files.FirstOrDefault(f => f.Primary) ?? v.Files.First();
        var destination = Path.GetFullPath(Path.Combine("work", "v015-real-packs", ContentService.SafeFile(file.Filename)));
        await packs.Download(file.Url, destination, file.Hashes["sha512"], Console.WriteLine);
        return destination;
    }

    async Task<string> Install(PackRequirements r) => await new GameInstaller(live, root, "", Console.WriteLine).Install(r);
    var old = await packs.Import(await Archive(versions[1]), root, Install, false, Console.WriteLine);
    var oldDir = catalog.DirectoryFor(old.InstanceId);
    catalog.GetOrCreate(old.InstanceId);
    Directory.CreateDirectory(Path.Combine(oldDir, "config"));
    File.WriteAllText(Path.Combine(oldDir, "config", "mcuglauncher-player-test.txt"), "player-custom-config");
    using var upgrade = await PackUpgrade.Prepare(live, root, old.InstanceId, oldDir, await Archive(versions[0]), Install, false, Console.WriteLine, default);
    var key = await PackUpgrade.Commit(upgrade, upgrade.Files.Where(f => f.KeepOld).Select(f => f.Relative), new(), Console.WriteLine, default);
    if (File.ReadAllText(Path.Combine(catalog.DirectoryFor(key), "config", "mcuglauncher-player-test.txt")) != "player-custom-config")
        throw new Exception("Custom config not preserved");
    await Run(key, "REAL_PACK_UPGRADED");
    await Run(old.InstanceId, "REAL_PACK_ORIGINAL");
    Console.WriteLine("REAL_PACK_UPGRADE_AND_RETURN_PASSED " + key);
    return;
}

if (args.Contains("--matrix") || args.Contains("--launch-matrix"))
{
    using var live = new HttpClient
    {
        Timeout = TimeSpan.FromMinutes(3)
    };
    var matrix = new[] {
        new PackRequirements("1.21.1", "vanilla", ""),
        new PackRequirements("1.21.1", "fabric", "0.19.5"),
        new PackRequirements("1.21.1", "quilt", "0.30.1"),
        new PackRequirements("1.21.1", "forge", "52.1.20"),
        new PackRequirements("1.21.1", "neoforge", "21.1.264")
    };
    foreach (var requirements in matrix)
    {
        Console.WriteLine("MATRIX_INSTALL_START " + requirements.Loader);
        var lastReport = DateTime.MinValue;
        var cachedProfiles = new Dictionary<string, string>
        {
            ["vanilla"] = "1.21.1",
            ["fabric"] = "fabric-loader-0.19.5-1.21.1",
            ["quilt"] = "quilt-loader-0.30.1-1.21.1",
            ["forge"] = "1.21.1-forge-52.1.20",
            ["neoforge"] = "neoforge-21.1.264"
        };
        var profile = args.Contains("--launch-matrix") ? cachedProfiles[requirements.Loader] : await new GameInstaller(live, root, "", text =>
        {
            if ((DateTime.UtcNow - lastReport).TotalSeconds >= 10)
            {
                Console.WriteLine(text);
                lastReport = DateTime.UtcNow;
            }
        }).Install(requirements);
        if (!VersionInstallation.IsReady(root, profile))
            throw new Exception("Installed profile is not ready");
        Console.WriteLine("MATRIX_INSTALL_READY " + profile);
        var key = catalog.Create("0.14 兼容性验收 " + requirements.Loader, profile, requirements.Minecraft);
        var descriptor = catalog.GetOrCreate(key);
        descriptor.DataDirectory = Path.GetFullPath(Path.Combine("work", "v0141 中文 空格路径", key));
        catalog.Save(descriptor);
        await Run(key, "MATRIX_" + requirements.Loader.ToUpperInvariant());
    }

    Console.WriteLine("ALL_FIVE_INSTALL_AND_LAUNCH_MATRIX_PASSED");
    return;
}

if (args.Contains("--legacy"))
{
    using var live = new HttpClient
    {
        Timeout = TimeSpan.FromMinutes(3)
    };
    using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(5));
    var lastReport = DateTime.MinValue;
    var profile = await new GameInstaller(live, root, "", text =>
    {
        if ((DateTime.UtcNow - lastReport).TotalSeconds >= 10)
        {
            Console.WriteLine(text);
            lastReport = DateTime.UtcNow;
        }
    }, deadline.Token).Install(new("1.12.2", "vanilla", ""));
    var key = catalog.Create("0.14 旧版 1.12.2 验收", profile, "1.12.2");
    var descriptor = catalog.GetOrCreate(key);
    descriptor.DataDirectory = Path.GetFullPath(Path.Combine("work", "v0141 中文 空格路径", key));
    catalog.Save(descriptor);
    await Run(key, "LEGACY_1_12_2", "1.12.2");
    Console.WriteLine("LEGACY_INSTALL_AND_LAUNCH_PASSED");
    return;
}

if (args.FirstOrDefault() == "--clone-game")
{
    var sourceKey = catalog.Create("0.8 copy source", "1.21.1", "1.21.1");
    var source = catalog.DirectoryFor(sourceKey);
    Directory.CreateDirectory(Path.Combine(source, "config"));
    File.WriteAllText(Path.Combine(source, "config", "copy-proof.txt"), "original");
    var copyKey = await InstanceCloner.Copy(root, sourceKey, source, "1.21.1", "0.8 copy launch", new LaunchSettings(), new());
    if (File.ReadAllText(Path.Combine(catalog.DirectoryFor(copyKey), "config", "copy-proof.txt")) != "original")
        throw new Exception("Clone content missing");
    Console.WriteLine("CLONE_REAL_INSTANCE_CREATED_OK");
    await Run(copyKey, "CLONED_VANILLA");
    return;
}

if (args.FirstOrDefault() == "--loaders")
{
    using var loaderHttp = new HttpClient
    {
        Timeout = TimeSpan.FromMinutes(3)
    };
    foreach (var loader in args.Skip(1).DefaultIfEmpty("forge"))
    {
        var releases = await LoaderCatalog.Get(loaderHttp, "1.21.1", loader, CancellationToken.None);
        var release = releases.First(r => !r.Version.Contains("beta"));
        Console.WriteLine($"INSTALL_{loader}_{release.Version}");
        var profile = await new GameInstaller(loaderHttp, root, "", Console.WriteLine).Install(new("1.21.1", loader, release.Version));
        Console.WriteLine(loader.ToUpperInvariant() + "_INSTALL_OK: " + profile);
        var key = catalog.Create("0.6 " + loader + " integration", profile, "1.21.1");
        await Run(key, loader.ToUpperInvariant());
    }

    return;
}

if (!args.Contains("--pack-only"))
{
    var a = catalog.Create("0.4 vanilla A", "1.21.1", "1.21.1");
    var b = catalog.Create("0.4 vanilla B", "1.21.1", "1.21.1");
    File.WriteAllText(Path.Combine(catalog.DirectoryFor(a), "isolation-proof.txt"), "instance A");
    if (File.Exists(Path.Combine(catalog.DirectoryFor(b), "isolation-proof.txt")))
        throw new Exception("Instance isolation failed");
    Console.WriteLine("TWO_SAME_VERSION_INSTANCES_ISOLATED_OK");
    await Run(a, "VANILLA");
    if (args.Contains("--vanilla-only"))
        return;
}

var packFile = Directory.GetFiles(Path.Combine(root, "instances"), ".mcuglauncher-pack.json", SearchOption.AllDirectories).First(f => JsonSerializer.Deserialize<InstalledPack>(File.ReadAllText(f))?.Loader == "quilt");
var pack = JsonSerializer.Deserialize<InstalledPack>(File.ReadAllText(packFile))!;
await Run(pack.InstanceId, "MODPACK");
var packDirectory = catalog.DirectoryFor(pack.InstanceId);
var exported = Path.GetFullPath("work/v0141-roundtrip-pack.zip");
await InstanceManagement.Export(packDirectory, exported, pack.Name, new(pack.Minecraft, pack.Loader, pack.LoaderVersion), new(true, false, true, true, false), CancellationToken.None);
using var http = new HttpClient();
var roundTrip = await new PackService(http).Import(exported, root, r =>
{
    if (r.Minecraft != pack.Minecraft || r.Loader != pack.Loader || r.LoaderVersion != pack.LoaderVersion)
        throw new Exception("Pack requirements mismatch");
    return Task.FromResult(pack.LaunchVersion);
}, true);
await Run(roundTrip.InstanceId, "REIMPORTED_MODPACK");
Console.WriteLine("ALL_GAME_REGRESSIONS_PASSED");
async Task Run(string key, string label, string minecraft = "1.21.1", int ram = 3072, AuthlibLaunch? external = null)
{
    var instance = catalog.GetOrCreate(key);
    var directory = catalog.DirectoryFor(key);
    GameDefaults.EnsureChinese(directory, minecraft);
    var settings = new LaunchSettings
    {
        AutoMemory = false,
        RamMb = ram,
        Width = 854,
        Height = 480,
        JvmArguments = "-Dmcuglauncher.test=true",
        VersionInfo = "MCUGLauncher-test"
    };
    var discovered = await JavaDiscovery.FindCompatible(root, instance.LaunchProfileId, CancellationToken.None);
    var identity = external == null ? MSession.CreateOfflineSession("StableTester") : new MSession
    {
        Username = "MuaFixture",
        UUID = "0123456789abcdef0123456789abcdef",
        AccessToken = "mua-test-not-real-token",
        UserType = "mojang"
    };
    var process = await LaunchService.Build(new(root, directory, instance.LaunchProfileId, "", ram, 854, 480, identity, settings, external), CancellationToken.None);
    if (!string.IsNullOrWhiteSpace(discovered) && !Path.GetFullPath(process.StartInfo.FileName).Equals(Path.GetFullPath(discovered), StringComparison.OrdinalIgnoreCase))
        throw new Exception("Discovered Java was not selected");
    if (!process.StartInfo.CreateNoWindow || process.StartInfo.UseShellExecute || !process.StartInfo.RedirectStandardOutput || !process.StartInfo.RedirectStandardError)
        throw new Exception("Console suppression or log capture configuration failed");
    Console.WriteLine(label + "_NO_CONSOLE_AND_LOG_CAPTURE_CONFIG_OK");
    if (string.IsNullOrWhiteSpace(discovered))
    {
        var expected = JavaDiscovery.RequiredMajor(root, instance.LaunchProfileId);
        if (expected > 0 && await JavaDiscovery.ProbeMajor(process.StartInfo.FileName, CancellationToken.None) != expected)
            throw new Exception("Managed Java mismatch");
        Console.WriteLine(label + "_MANAGED_JAVA_RUNTIME_OK");
    }
    else
        Console.WriteLine(label + "_LOCAL_JAVA_AUTO_DISCOVERY_OK");
    var command = process.StartInfo.Arguments + string.Join(" ", process.StartInfo.ArgumentList);
    if (external != null && (!command.Contains("-javaagent:") || !command.Contains(AuthlibLaunch.Api) || !command.Contains("-Dauthlibinjector.yggdrasil.prefetched=")))
        throw new Exception("MUA injection arguments missing");
    if (!command.Contains("-Dmcuglauncher.test=true") || !command.Contains("MCUGLauncher-test"))
        throw new Exception("Advanced arguments not applied");
    Console.WriteLine(label + "_ADVANCED_LAUNCH_ARGUMENTS_OK");
    var captured = Path.GetFullPath("work/v0141-" + label.ToLowerInvariant() + "-process.log");
    var session = new GameSession(process, captured, _ =>
    {
    }, ["access_token", "mua-test-not-real-token"]);
    var started = DateTime.UtcNow;
    session.Start();
    var rendered = false;
    try
    {
        var latest = Path.Combine(directory, "logs", "latest.log");
        for (var i = 0; i < (label.StartsWith("LARGE_") ? 180 : 60); i++)
        {
            await Task.Delay(2000);
            if (process.HasExited)
                throw new Exception(label + " exited: " + process.ExitCode);
            if (!File.Exists(latest) || File.GetLastWriteTimeUtc(latest) < started)
                continue;
            var text = SharedLogs.ReadTail(latest);
            if (text.Contains("OpenGL") || text.Contains("Backend library: LWJGL") || text.Contains("LWJGL Version:") && text.Contains("textures-atlas"))
            {
                rendered = true;
                break;
            }
        }

        if (!rendered)
            throw new Exception(label + " did not reach renderer");
        await Task.Delay(8000);
        if (process.HasExited)
            throw new Exception(label + " exited after render init");
        Console.WriteLine(label + "_RENDER_AND_SHARED_LOG_READ_OK");
        var requested = session.RequestStop();
        Console.WriteLine(label + "_CLOSE_REQUEST_SENT=" + requested + "; window=" + process.MainWindowTitle);
        try
        {
            var result = await session.Completion.WaitAsync(TimeSpan.FromSeconds(12));
            if (result.Code != 0)
                throw new Exception(label + " abnormal exit " + result.Code);
            Console.WriteLine($"{label}_EXIT_OBSERVED_OK: {result.Code}, requested={result.Requested}");
        }
        catch (TimeoutException)
        {
            process.Kill(true);
            await session.Completion;
            Console.WriteLine(label + "_TEST_CLEANUP_KILLED");
            throw new Exception(label + " did not close normally within deadline");
        }
    }
    finally
    {
        if (!process.HasExited)
        {
            process.Kill(true);
            await session.Completion;
        }
    }
}
