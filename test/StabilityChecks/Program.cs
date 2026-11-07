using BlockLaunch;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.IO.Compression;

if (args.FirstOrDefault() == "--resources")
{
    using var live = new HttpClient();
    live.DefaultRequestHeaders.UserAgent.ParseAdd("MCUGLauncher/0.5 (desktop-resource-verification)");
    var service = new ContentService(live);
    foreach (var kind in new[] {
        "mod",
        "resourcepack",
        "shader",
        "datapack",
        "modpack"
    }

    )
    {
        var page = await service.Search("", "1.21.1", kind == "datapack" ? "datapack" : "fabric", kind);
        if (page.Items.Count == 0)
            throw new Exception("No resources: " + kind);
        var versions = await service.Versions(page.Items[0].Id, "1.21.1", kind == "datapack" ? "datapack" : "fabric", kind);
        if (versions.Count == 0)
            throw new Exception("No versions: " + kind);
        if (kind is "shader" or "datapack")
        {
            var plan = await service.Resolve(versions[0], "1.21.1", kind == "datapack" ? "datapack" : "fabric");
            if (!plan.Types.Values.Contains(kind))
                throw new Exception("Wrong destination type: " + kind);
        }

        Console.WriteLine("LIVE_RESOURCE_SEARCH_AND_VERSIONS_" + kind.ToUpperInvariant() + "_OK");
    }

    return;
}

if (args.FirstOrDefault() == "--reflect")
{
    foreach (var p in typeof(CmlLib.Core.ProcessBuilder.MArgument).GetConstructors())
        Console.WriteLine(p);
    foreach (var p in typeof(CmlLib.Core.ProcessBuilder.MArgument).GetMethods().Where(m => m.IsStatic))
        Console.WriteLine(p);
    return;
}

if (args.FirstOrDefault() is "--crash-content" or "--crash-data")
{
    var transaction = new ContentTransaction(args[1]);
    var relative = args[0] == "--crash-data" ? "datapacks/demo.zip" : "mods/demo.jar";
    File.WriteAllText(Path.Combine(transaction.Stage, "new.jar"), "new-content");
    transaction.Backup(relative);
    transaction.Write(Path.Combine(transaction.Stage, "new.jar"), relative);
    Environment.Exit(23);
}

if (args.FirstOrDefault() == "--crash-task")
{
    var childCoordinator = new OperationCoordinator(args[1]);
    await childCoordinator.Run("被中断的测试", args[1], "1.21.1", _ =>
    {
        Environment.Exit(24);
        return Task.CompletedTask;
    });
    return;
}

if (args.FirstOrDefault() == "--output")
{
    Console.WriteLine("--accessToken secret-test-token");
    Console.Error.WriteLine("stderr-line");
    Environment.Exit(7);
}

var root = Path.GetFullPath("work/stability-check-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
void Assert(bool condition, string label)
{
    if (!condition)
        throw new Exception(label);
    Console.WriteLine(label + " OK");
}

var settingsDirectory = Path.Combine(root, "settings");
Directory.CreateDirectory(settingsDirectory);
var settingsFile = Path.Combine(settingsDirectory, "settings.json");
var gameRoot = Path.Combine(root, "minecraft");
AtomicFile.WriteJson(settingsFile, new { Root = gameRoot, Ram = 6144, Player = "Tester", Width = 1280, Height = 720, LastVersion = "1.21.1" });
var settings = new SettingsStore(settingsDirectory);
var prefs = settings.Load();
Assert(prefs.SchemaVersion == 2 && prefs.Ram == 6144 && File.Exists(settingsFile + ".v1.backup"), "LEGACY_CONFIG_BACKUP_MIGRATION");
File.WriteAllText(settingsFile, "broken-json");
settings.Load();
Assert(Directory.GetFiles(settingsDirectory, "settings.json.corrupt-*").Length == 1, "CORRUPT_CONFIG_PRESERVED");
AtomicFile.WriteJson(settingsFile, new Preferences { SchemaVersion = 999 });
var future = File.ReadAllText(settingsFile);
try
{
    settings.Load();
    throw new Exception("Future schema accepted");
}
catch (NotSupportedException)
{
}

Assert(File.ReadAllText(settingsFile) == future, "FUTURE_SCHEMA_NOT_OVERWRITTEN");
var catalog = new InstanceCatalog(gameRoot);
var old = catalog.GetOrCreate("1.21.1");
Assert(new InstanceCatalog(gameRoot).GetOrCreate("1.21.1").InstanceId == old.InstanceId, "INSTANCE_ID_STABLE");
var first = catalog.Create("First", "1.21.1", "1.21.1");
var second = catalog.Create("Second", "1.21.1", "1.21.1");
File.WriteAllText(Path.Combine(catalog.DirectoryFor(first), "saves.txt"), "first");
Assert(catalog.GetOrCreate(first).InstanceId != catalog.GetOrCreate(second).InstanceId && !File.Exists(Path.Combine(catalog.DirectoryFor(second), "saves.txt")), "SAME_PROFILE_INSTANCES_ISOLATED");
var coordinator = new OperationCoordinator(Path.Combine(root, "tasks"));
var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var cancel = coordinator.Run("取消测试", gameRoot, first, async context =>
{
    started.SetResult();
    await Task.Delay(Timeout.Infinite, context.Token);
});
await started.Task;
Assert(coordinator.Cancel(), "CANCEL_REQUEST_ACCEPTED");
var cancelled = await cancel;
Assert(cancelled.State == OperationState.Cancelled && coordinator.Current == null, "CANCEL_FINISHES_AFTER_CLEANUP");
await coordinator.Run("提交保护", gameRoot, first, context =>
{
    using var guard = context.BeginCommit();
    Assert(!coordinator.Cancel(), "COMMIT_IS_NOT_CANCELLABLE");
    return Task.CompletedTask;
});
await Child("--crash-task", Path.Combine(root, "crashed-tasks"));
var recovery = new OperationCoordinator(Path.Combine(root, "crashed-tasks"));
Assert(recovery.History().Single().State == OperationState.Interrupted, "REAL_PROCESS_TASK_INTERRUPTION_DETECTED");
using var cts = new CancellationTokenSource();
var slowHandler = new SlowHandler();
using var http = new HttpClient(slowHandler);
var content = new ContentService(http, cts.Token);
var bytes = Encoding.UTF8.GetBytes("not-yet-downloaded");
var version = new ContentVersion
{
    Id = "version",
    ProjectId = "project",
    Name = "Slow",
    Files = [new()
    {
        Filename = "slow.jar",
        Url = "https://cdn.modrinth.com/test/slow.jar",
        Size = bytes.Length,
        Hashes = new()
        {
            ["sha512"] = Convert.ToHexString(SHA512.HashData(bytes))
        }
    }

    ]
};
var instance = catalog.DirectoryFor(first);
var pending = content.Install(new([version], new()
{
    ["project"] = "mod"
}), instance);
await Task.WhenAny(pending, slowHandler.Started.Task).WaitAsync(TimeSpan.FromSeconds(15));
if (pending.IsCompleted)
    await pending;
await slowHandler.Started.Task;
cts.Cancel();
try
{
    await pending;
    throw new Exception("Expected download cancellation");
}
catch (OperationCanceledException)
{
}

Assert(!File.Exists(Path.Combine(instance, "mods", "slow.jar")) && !File.Exists(Path.Combine(instance, ".blocklaunch-content.json")), "CANCELLED_DOWNLOAD_NO_LIVE_FILES");
await using (var stream = new FileStream(Path.Combine(root, "live.log"), FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
{
    var data = Encoding.UTF8.GetBytes("{\"access_token\":\"very-private-token\"}\nline two\n");
    await stream.WriteAsync(data);
    await stream.FlushAsync();
    var text = SharedLogs.ReadTail(Path.Combine(root, "live.log"));
    Assert(text.Contains("line two") && !text.Contains("very-private-token"), "LOG_READ_WHILE_WRITER_OPEN_AND_REDACT");
}

Assert(!LogPrivacy.Redact("--accessToken secret-token").Contains("secret-token"), "COMMAND_TOKEN_REDACTED");
Directory.CreateDirectory(Path.Combine(instance, "mods"));
File.WriteAllText(Path.Combine(instance, "mods", "demo.jar"), "original-content");
await Child("--crash-content", instance);
Assert(File.ReadAllText(Path.Combine(instance, "mods", "demo.jar")) == "new-content", "CRASH_FIXTURE_MUTATED_LIVE_FILE");
ContentTransaction.Recover(instance);
Assert(File.ReadAllText(Path.Combine(instance, "mods", "demo.jar")) == "original-content", "REAL_PROCESS_INSTALL_TRANSACTION_ROLLED_BACK");
Assert(ContentTransaction.Recover(instance).Count == 0, "RECOVERY_IDEMPOTENT");
using var cancelledInstall = new CancellationTokenSource();
cancelledInstall.Cancel();
try
{
    await VersionInstallation.Run(gameRoot, "test-version", ct => Task.Delay(1, ct), cancelledInstall.Token);
}
catch (OperationCanceledException)
{
}

Assert(!VersionInstallation.IsReady(gameRoot, "test-version"), "CANCELLED_GAME_NOT_MARKED_READY");
await VersionInstallation.Run(gameRoot, "test-version", _ => Task.CompletedTask, CancellationToken.None);
Assert(VersionInstallation.IsReady(gameRoot, "test-version"), "RETRY_GAME_MARKS_READY");
var outputFile = Path.Combine(root, "process.log");
var lines = new List<string>();
var session = new GameSession(StartInfo("--output", ""), outputFile, line =>
{
    lock (lines)
        lines.Add(line);
}, ["secret-test-token"]);
session.Start();
var exit = await session.Completion;
Assert(exit.Code == 7 && !exit.Requested && !File.ReadAllText(outputFile).Contains("secret-test-token") && lines.Contains("stderr-line"), "PROCESS_EXIT_OUTPUT_AND_PRIVACY");
using var fixtureHttp = new HttpClient(new FixtureHandler());
var installer = new ContentService(fixtureHttp);
var fixtureBytes = Encoding.UTF8.GetBytes("fixture-content");
ContentVersion Fixture(string id, string filename) => new()
{
    Id = id,
    ProjectId = "fixture",
    Name = "Fixture",
    Files = [new()
    {
        Filename = filename,
        Url = "https://cdn.modrinth.com/fixture",
        Size = fixtureBytes.Length,
        Hashes = new()
        {
            ["sha512"] = Convert.ToHexString(SHA512.HashData(fixtureBytes))
        }
    }

    ]
};
InstallPlan Plan(ContentVersion value) => new([value], new()
{
    ["fixture"] = "mod"
});
await installer.Install(Plan(Fixture("one", "fixture-one.jar")), instance);
await installer.Install(Plan(Fixture("two", "fixture-two.jar")), instance);
Assert(!File.Exists(Path.Combine(instance, "mods", "fixture-one.jar")) && ContentService.ReadManaged(instance).Single().VersionId == "two", "CONTENT_REINSTALL_UPDATES_FILE_AND_MANIFEST");
var invalid = Fixture("bad", "bad.jar");
invalid.Files[0].Hashes["sha512"] = new string('0', 128);
try
{
    await installer.Install(Plan(invalid), instance);
    throw new Exception("Invalid hash accepted");
}
catch (IOException)
{
}

Assert(File.Exists(Path.Combine(instance, "mods", "fixture-two.jar")) && ContentService.ReadManaged(instance).Single().VersionId == "two", "HASH_FAILURE_PRESERVES_INSTALLED_CONTENT");
// Isolated update fixtures: no user instances or saves are touched.
var updateInstance = Path.Combine(root, "disabled-update");
var updateHandler = new FixtureHandler();
using var updateHttp = new HttpClient(updateHandler);
var updater = new ContentService(updateHttp);
await updater.Install(Plan(Fixture("old", "old.jar")), updateInstance);
ContentService.Toggle(ContentService.ListLocal(updateInstance).Single());
await updater.Install(Plan(Fixture("new", "renamed.jar")), updateInstance);
Assert(!File.Exists(Path.Combine(updateInstance, "mods", "renamed.jar")) && File.Exists(Path.Combine(updateInstance, "mods", "renamed.jar.disabled")) && !File.Exists(Path.Combine(updateInstance, "mods", "old.jar.disabled")), "DISABLED_UPDATE_PRESERVES_STATE_WHEN_RENAMED");
await updater.Install(Plan(Fixture("same-name", "renamed.jar")), updateInstance);
Assert(ContentService.ListLocal(updateInstance).Single().State == "已禁用" && ContentService.ReadManaged(updateInstance).Single().VersionId == "same-name", "DISABLED_UPDATE_SAME_NAME_AND_MANIFEST");
var disabledFile = Path.Combine(updateInstance, "mods", "renamed.jar.disabled");
File.WriteAllText(disabledFile, "user-edit");
var beforeRequests = updateHandler.Requests;
var blocked = Fixture("blocked", "blocked.jar");
blocked.Files[0].Hashes["sha512"] = new string('f', 128);
try
{
    await updater.Install(Plan(blocked), updateInstance);
    throw new Exception("Modified content accepted");
}
catch (IOException ex)
{
    Assert(ex.Message.Contains("被修改"), "MODIFIED_CONTENT_REJECTED_IN_PREFLIGHT");
}

Assert(updateHandler.Requests == beforeRequests && File.ReadAllText(disabledFile) == "user-edit" && ContentService.ReadManaged(updateInstance).Single().VersionId == "same-name", "PREFLIGHT_NO_NETWORK_OR_MANIFEST_CHANGE");
File.WriteAllBytes(disabledFile, fixtureBytes);
File.WriteAllBytes(Path.Combine(updateInstance, "mods", "renamed.jar"), fixtureBytes);
try
{
    await updater.Install(Plan(Fixture("ambiguous", "another.jar")), updateInstance);
    throw new Exception("Ambiguous content accepted");
}
catch (IOException ex)
{
    Assert(ex.Message.Contains("同时存在"), "AMBIGUOUS_ENABLED_DISABLED_REJECTED");
}

File.Delete(Path.Combine(updateInstance, "mods", "renamed.jar"));
try
{
    await updater.Install(Plan(Fixture("rollback", "blocked-target.jar")), updateInstance, _ => Directory.CreateDirectory(Path.Combine(updateInstance, "mods", "blocked-target.jar.disabled")));
    throw new Exception("Blocked target accepted");
}
catch (IOException)
{
}

Assert(File.Exists(disabledFile) && File.ReadAllBytes(disabledFile).SequenceEqual(fixtureBytes) && ContentService.ReadManaged(updateInstance).Single().VersionId == "same-name", "FAILED_DISABLED_UPDATE_RESTORES_OLD_FILE_AND_MANIFEST");
Directory.Delete(Path.Combine(updateInstance, "mods", "blocked-target.jar.disabled"));
ContentService.Toggle(ContentService.ListLocal(updateInstance).Single());
await updater.Install(Plan(Fixture("enabled", "enabled.jar")), updateInstance);
Assert(File.Exists(Path.Combine(updateInstance, "mods", "enabled.jar")) && ContentService.ListLocal(updateInstance).Single().State == "已启用", "ENABLED_UPDATE_REMAINS_ENABLED");
var localJar = Path.Combine(root, "local.jar");
File.WriteAllText(localJar, "local-content");
Assert(await installer.ImportFiles(instance, "mods", [localJar, localJar]) == 1 && await installer.ImportFiles(instance, "mods", [localJar]) == 0, "LOCAL_IMPORT_SKIPS_DUPLICATES");
var orphanAlias = "pack-123456789012";
var orphanStage = Path.Combine(gameRoot, ".blocklaunch-staging", "orphan");
Directory.CreateDirectory(orphanStage);
var orphanInstance = Path.Combine(gameRoot, "instances", orphanAlias);
Directory.CreateDirectory(orphanInstance);
AtomicFile.WriteJson(Path.Combine(orphanInstance, ".blocklaunch-pack.json"), new InstalledPack("Test", "1.21.1", "vanilla", "", "1.21.1", orphanAlias));
AtomicFile.WriteJson(Path.Combine(orphanStage, "pack-operation.json"), new PackCommitRecord(orphanAlias, "Preparing"));
InstallationRecovery.Recover(gameRoot);
Assert(!Directory.Exists(orphanInstance) && Directory.Exists(Path.Combine(orphanStage, "recovered-instance")), "ORPHAN_PACK_DATA_PRESERVED_IN_RECOVERY");
var instanceRecord = catalog.GetOrCreate(first);
instanceRecord.DisplayName = "我的收藏";
instanceRecord.Favorite = true;
instanceRecord.Category = "生存";
instanceRecord.Description = "Description";
instanceRecord.Launch = new LaunchSettings
{
    AutoMemory = false,
    RamMb = 6144,
    JvmArguments = "-Dtest=hello \"-Dmessage=hello world\""
};
catalog.Save(instanceRecord);
var reloaded = new InstanceCatalog(gameRoot).GetOrCreate(first);
Assert(reloaded.Favorite && reloaded.DisplayName == "我的收藏" && reloaded.Launch?.RamMb == 6144, "INSTANCE_CUSTOMIZATION_AND_SETTINGS_PERSIST");
Assert(LaunchSettings.SplitArguments(instanceRecord.Launch.JvmArguments).Count == 2, "JVM_QUOTED_ARGUMENTS_PRESERVED");
try
{
    LaunchSettings.SplitArguments("-Xmx1G");
    throw new Exception("Memory override accepted");
}
catch (ArgumentException)
{
}

Assert(MemoryPolicy.Recommend() is >= 1024 and <= 8192, "AUTO_MEMORY_BOUNDED");
Directory.CreateDirectory(Path.Combine(instance, "config"));
File.WriteAllText(Path.Combine(instance, "config", "test.toml"), "configuration");
File.WriteAllText(Path.Combine(instance, "servers.dat"), "private-server");
Directory.CreateDirectory(Path.Combine(instance, "shaderpacks"));
File.WriteAllText(Path.Combine(instance, "shaderpacks", "shader.zip"), "shader");
var exported = Path.Combine(root, "export.zip");
Directory.CreateDirectory(Path.Combine(instance, "accounts"));
File.WriteAllText(Path.Combine(instance, "accounts", "microsoft-account.dat"), "protected-account-fixture");
await InstanceManagement.Export(instance, exported, "Round trip pack", new("1.21.1", "fabric", "0.16.10"), new(false, false, true, false, false), CancellationToken.None);
using (var zip = ZipFile.OpenRead(exported))
    Assert(zip.GetEntry("mmc-pack.json") != null && zip.GetEntry("minecraft/config/test.toml") != null && zip.GetEntry("minecraft/mods/demo.jar") == null && zip.GetEntry("minecraft/servers.dat") == null && zip.GetEntry("minecraft/shaderpacks/shader.zip") != null && zip.Entries.All(e => !e.FullName.Contains(".blocklaunch")), "PACK_EXPORT_FILTERS_AND_MANIFEST");
using (var zip = ZipFile.OpenRead(exported))
    Assert(zip.Entries.All(e => !e.FullName.Contains("microsoft-account.dat")), "PACK_EXPORT_EXCLUDES_ACCOUNT_CACHE");
var imported = await new PackService(fixtureHttp).Import(exported, gameRoot, requirements =>
{
    Assert(requirements.Loader == "fabric" && requirements.LoaderVersion == "0.16.10", "EXPORTED_COMPONENTS_PARSED");
    return Task.FromResult("1.21.1");
}, true);
Assert(imported.Name == "Round trip pack" && File.Exists(Path.Combine(gameRoot, "instances", imported.InstanceId, "config", "test.toml")), "EXPORTED_PACK_REIMPORTS");
var trash = InstanceManagement.Remove(gameRoot, second);
Assert(!Directory.Exists(catalog.MetadataDirectory(second)) && Directory.Exists(Path.Combine(trash, "instance")), "INSTANCE_REMOVAL_PRESERVES_BACKUP");
var shaderFixture = Fixture("shader", "fixture-shader.zip");
shaderFixture.ProjectId = "shader-project";
await installer.Install(new([shaderFixture], new()
{
    [shaderFixture.ProjectId] = "shader"
}), instance);
var dataFixture = Fixture("data", "fixture-data.zip");
dataFixture.ProjectId = "data-project";
var world = Path.Combine(instance, "saves", "world");
Directory.CreateDirectory(world);
await installer.Install(new([dataFixture], new()
{
    [dataFixture.ProjectId] = "datapack"
}), world);
Assert(File.Exists(Path.Combine(instance, "shaderpacks", "fixture-shader.zip")) && File.Exists(Path.Combine(world, "datapacks", "fixture-data.zip")), "SHADER_AND_DATAPACK_DESTINATIONS");
File.WriteAllText(Path.Combine(world, "datapacks", "demo.zip"), "original-data");
await Child("--crash-data", world);
InstallationRecovery.Recover(gameRoot);
Assert(File.ReadAllText(Path.Combine(world, "datapacks", "demo.zip")) == "original-data", "WORLD_DATAPACK_TRANSACTION_RECOVERED_AT_STARTUP");
Console.WriteLine("ALL_STABILITY_CHECKS_PASSED");
Process StartInfo(string mode, string path)
{
    var info = new ProcessStartInfo(Environment.ProcessPath!)
    {
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };
    if (string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath), "dotnet", StringComparison.OrdinalIgnoreCase))
        info.ArgumentList.Add(typeof(Program).Assembly.Location);
    info.ArgumentList.Add(mode);
    if (path.Length > 0)
        info.ArgumentList.Add(path);
    return new()
    {
        StartInfo = info
    };
}

async Task Child(string mode, string path)
{
    using var child = StartInfo(mode, path);
    child.Start();
    await child.WaitForExitAsync();
    Assert(child.ExitCode is 23 or 24, "CHILD_FAULT_INJECTION");
}

sealed class SlowHandler : HttpMessageHandler
{
    /// <summary>
    /// 获取或设置 Started。
    /// </summary>
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// 发送。
    /// </summary>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken token)
    {
        Started.TrySetResult();
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new WaitingStream()) });
    }
}

sealed class FixtureHandler : HttpMessageHandler
{
    public int Requests
    {
        get;
        private set;
    }

    /// <summary>
    /// 发送。
    /// </summary>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        Requests++;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes("fixture-content")) });
    }
}

sealed class WaitingStream : Stream
{
    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => 0;
        set => throw new NotSupportedException();
    }

    /// <summary>
    /// 读取。
    /// </summary>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
    {
        await Task.Delay(Timeout.Infinite, token);
        return 0;
    }

    /// <summary>
    /// 读取。
    /// </summary>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token) => ReadAsync(buffer.AsMemory(offset, count), token).AsTask();
    /// <summary>
    /// 读取。
    /// </summary>
    public override int Read(byte[] b, int o, int c) => throw new NotSupportedException();

    /// <summary>
    /// 执行 Flush。
    /// </summary>
    public override void Flush()
    {
    }

    /// <summary>
    /// 执行 Seek。
    /// </summary>
    public override long Seek(long o, SeekOrigin s) => throw new NotSupportedException();
    /// <summary>
    /// 设置Length。
    /// </summary>
    public override void SetLength(long v) => throw new NotSupportedException();
    /// <summary>
    /// 写入。
    /// </summary>
    public override void Write(byte[] b, int o, int c) => throw new NotSupportedException();
}
