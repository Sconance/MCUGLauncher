using System.IO.Compression;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using MCUGLauncher;
using MCUGLauncher.Models;
using MCUGLauncher.Services;
using MCUGLauncher.UseCases;

internal static class MaintenanceScenarios
{
    public static async Task Run(string fixture)
    {
        var root = Path.Combine(fixture, "maintenance-game");
        var vanilla = Path.Combine(root, "versions", "1.21.1");
        var source = Path.Combine(root, "versions", "fabric-fixture");
        Directory.CreateDirectory(vanilla);
        Directory.CreateDirectory(Path.Combine(source, "mods"));
        File.WriteAllText(Path.Combine(vanilla, "1.21.1.json"), """{"id":"1.21.1","type":"release"}""");
        File.WriteAllText(Path.Combine(source, "fabric-fixture.json"), """{"id":"fabric-fixture","inheritsFrom":"1.21.1","libraries":[{"name":"net.fabricmc:fabric-loader:0.16.0"}]}""");
        File.WriteAllText(Path.Combine(source, "mods", "fixture.jar"), "original-mod");
        File.WriteAllText(Path.Combine(source, "options.txt"), "original-options");
        var config = new AppConfig { Root = root, LastInstance = "fabric-fixture", LastVersion = "fabric-fixture" };
        var descriptor = new InstanceCatalog(root).GetOrCreate("fabric-fixture");
        descriptor.Launch = new LaunchSettings { AutoMemory = false, RamMb = 1536, JvmArguments = "-Dfixture=true" };
        new InstanceCatalog(root).Save(descriptor);
        var snapshot = InstanceSnapshot.Capture(config, "fabric-fixture");
        Check(snapshot.Requirements == new PackRequirements("1.21.1", "fabric", "0.16.0"), "installed metadata resolves inherited game and exact loader version");
        Check(snapshot.Settings.RamMb == 1536, "instance launch overrides survive snapshot capture");
        snapshot.Settings.Java = "changed-copy";
        Check(new InstanceCatalog(root).GetOrCreate("fabric-fixture").Launch!.Java == "", "captured launch settings do not mutate source settings");
        snapshot.Settings.Java = "";

        using var http = new HttpClient();
        var executor = new DownloadExecutor(http);
        var coordinator = new OperationCoordinator(Path.Combine(fixture, "maintenance-operations"));
        var cloneRequest = snapshot.Request();
        cloneRequest.Name = "Fixture copy";
        var queued = coordinator.Enqueue("clone", root, source, new DownloadRequest { Kind = "clone", Instance = cloneRequest });
        var persisted = coordinator.History().Single(r => r.Id == queued.Id);
        config.Root = Path.Combine(fixture, "must-not-be-used");
        await coordinator.Run(persisted.Name, persisted.Root, persisted.Target, task => executor.ExecuteAsync(persisted, task), persisted);
        var key = Directory.GetDirectories(Path.Combine(root, "instances")).Select(Path.GetFileName).Single(k => k!.StartsWith("instance-"))!;
        var cloned = new InstanceCatalog(root).DirectoryFor(key);
        Check(File.ReadAllText(Path.Combine(cloned, "mods", "fixture.jar")) == "original-mod" && !Directory.Exists(config.Root), "persisted clone keeps original root and bytes after selection changes");
        Check(new InstanceCatalog(root).GetOrCreate(key).DisplayName == "Fixture copy", "clone preserves requested display name");
        config.Root = root;
        var cloneSnapshot = InstanceSnapshot.Capture(config, key);
        var archive = Path.Combine(fixture, "fixture-export.zip");
        var export = new OperationRecord { Root = root, Target = cloned, Download = new DownloadRequest { Kind = "export", Instance = cloneSnapshot.Request(), Destination = archive } };
        await coordinator.Run("export", root, cloned, task => executor.ExecuteAsync(export, task));
        Check(PackService.ReadOverview(archive).Requirements == snapshot.Requirements, "exported pack records real game and loader requirements");
        using (var zip = ZipFile.OpenRead(archive))
            Check(zip.GetEntry("minecraft/options.txt") != null, "export includes selected instance data");
        var before = SHA256.HashData(File.ReadAllBytes(archive));
        try
        {
            await coordinator.Run("export collision", root, cloned, task => executor.ExecuteAsync(export, task));
            throw new Exception("export overwrote file");
        }
        catch (IOException) { Check(before.SequenceEqual(SHA256.HashData(File.ReadAllBytes(archive))), "export never overwrites an existing archive"); }

        var store = new ConfigStore(Path.Combine(fixture, "maintenance-config"));
        store.Save(config);
        var messages = new List<JsonElement>();
        using (var app = new LauncherApplication(value => messages.Add(FixtureMessages.Capture(value)), new FakeDesktop(), store))
        {
            await app.HandleAsync(JsonSerializer.Serialize(new
            {
                type = "previewUninstall",
                version = key
            }));
            var preview = messages.Single(m => m.GetProperty("type").GetString() == "uninstallPreview");
            await app.HandleAsync(JsonSerializer.Serialize(new
            {
                type = "applyUninstall",
                token = preview.GetProperty("token").GetString()
            }));
            Check(Directory.Exists(cloned), "native uninstall cancellation leaves instance data untouched");
        }
        var plan = InstanceUninstaller.Plan(root, key, cloneSnapshot.Settings);
        var uninstall = new OperationRecord { Root = root, Target = cloned, Download = new DownloadRequest { Kind = "uninstall", Instance = cloneSnapshot.Request() } };
        uninstall.Download.Instance!.Uninstall = plan;
        await coordinator.Run("uninstall fixture", root, cloned, task => executor.ExecuteAsync(uninstall, task));
        Check(!Directory.Exists(cloned) && File.ReadAllText(Path.Combine(source, "mods", "fixture.jar")) == "original-mod", "confirmed uninstall removes only the reviewed independent fixture");

        var optionsOnly = Path.Combine(root, "versions", "options-only");
        Directory.CreateDirectory(optionsOnly);
        File.WriteAllText(Path.Combine(optionsOnly, "options.txt"), "keep");
        var legacy = new InstanceCatalog(root).GetOrCreate("options-only");
        Check(legacy.DataDirectory == Path.GetFullPath(optionsOnly), "legacy options-only data directory is retained");
        await UpdatesAsync(fixture, store, source);
        await LaunchSnapshotAsync(fixture, store);
    }

    static async Task UpdatesAsync(string fixture, ConfigStore store, string source)
    {
        var original = new ManagedContent { ProjectId = "fixture-project", VersionId = "old", Name = "Fixture", RelativePath = "mods/fixture.jar", Sha512 = new string('b', 128) };
        File.WriteAllText(Path.Combine(source, ".mcuglauncher-content.json"), JsonSerializer.Serialize(new[] { original }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }));
        var messages = new List<JsonElement>();
        using var context = new LauncherContext(value => messages.Add(FixtureMessages.Capture(value)), store);
        var commands = new ContentUpdateCommands(context, _ => new FakeUpdates());
        await commands.CheckAsync("fabric-fixture");
        var check = messages.Last();
        await commands.PreviewAsync(JsonSerializer.SerializeToElement(new
        {
            token = check.GetProperty("token").GetString(),
            projects = new[] { "fixture-project" }
        }));
        var preview = messages.Last();
        Check(preview.GetProperty("type").GetString() == "contentUpdatePreview", "content updates expose an explicit file-change preview");
        File.WriteAllText(Path.Combine(source, "mods", "fixture.jar"), "user-changed-after-preview");
        commands.Apply(preview.GetProperty("token").GetString()!);
        await context.Tasks.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        Check(context.Tasks.History().Single().State == OperationState.Failed && File.ReadAllText(Path.Combine(source, "mods", "fixture.jar")) == "user-changed-after-preview", "stale update fingerprint fails before replacing user content");
        try
        {
            commands.Apply(preview.GetProperty("token").GetString()!);
            throw new Exception("preview token reused");
        }
        catch (InvalidOperationException) { Check(true, "applied preview token cannot be reused"); }
    }

    static async Task LaunchSnapshotAsync(string fixture, ConfigStore store)
    {
        using var context = new LauncherContext(_ => { }, store);
        var expectedRoot = Path.GetFullPath(context.Config.Root);
        var expectedPlayer = context.Config.Player;
        var captured = new TaskCompletionSource<LaunchRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<Process>(TaskCreationOptions.RunContinuationsAsynchronously);
        var commands = new GameCommands(context, (request, _) => { captured.TrySetResult(request); return release.Task; });
        var launch = commands.LaunchAsync("fabric-fixture");
        var request = await captured.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Check(context.IsRootPreparing(expectedRoot), "launch preparation reserves root before awaiting process build");
        var operations = new OperationCoordinator(Path.Combine(fixture, "launch-guard"));
        using var http = new HttpClient();
        var job = new OperationRecord { Root = expectedRoot, Download = new DownloadRequest { Kind = "game", Version = "1.21.1" } };
        try
        {
            await operations.Run("guard", expectedRoot, "1.21.1", task => new DownloadExecutor(http, context.IsInstanceInUse, context.IsRootPreparing).ExecuteAsync(job, task));
            throw new Exception("installation overlapped launch preparation");
        }
        catch (IOException) { Check(true, "game installation cannot mutate a preparing launch root"); }
        context.Config.Root = Path.Combine(fixture, "changed-root");
        context.Config.Player = "ChangedAfterRequest";
        release.SetResult(new Process
        {
            StartInfo = new ProcessStartInfo("cmd.exe")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                ArgumentList = { "/c", "exit", "0" }
            }
        });
        await launch.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.WhenAll(context.Games.Active.Select(g => g.Session.Completion));
        Check(request.Root == expectedRoot && request.Session.Username == expectedPlayer && request.Ram == 1536,
            "launch freezes root identity and per-instance settings across asynchronous preparation");
        Check(!context.IsRootPreparing(expectedRoot), "launch reservation releases after process start");
    }

    static void Check(bool value, string label)
    {
        if (!value)
            throw new Exception(label);
        Console.WriteLine("PASS " + label);
    }

    sealed class FakeUpdates : IResourceProvider
    {
        static ContentVersion Version() => new() { Id = "new", ProjectId = "fixture-project", Name = "Fixture", VersionNumber = "2", VersionType = "release", GameVersions = ["1.21.1"], Loaders = ["fabric"], Files = [new ContentFile { Filename = "fixture-new.jar", Url = "https://cdn.modrinth.com/data/fixture-project/versions/new/fixture-new.jar", Size = 1, Primary = true, Hashes = new() { ["sha512"] = new string('a', 128) } }] };
        public Task<SearchPage> Search(string query, string game, string loader, string type, int offset = 0) => Task.FromResult(new SearchPage([], 0));
        public Task<List<ContentVersion>> Versions(string project, string game, string loader, string type) => Task.FromResult(new List<ContentVersion> { Version() });
        public Task<InstallPlan> Resolve(ContentVersion version, string game, string loader) => Task.FromResult(new InstallPlan([version], new() { [version.ProjectId] = "mod" }));
    }
}
