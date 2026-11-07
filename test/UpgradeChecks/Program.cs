using BlockLaunch;
using System.Net;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var root = Path.Combine(Path.GetTempPath(), "BlockLaunch-upgrades-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
void Check(bool ok, string label)
{
    if (!ok)
        throw new Exception(label);
    Console.WriteLine("PASS " + label);
}

async Task Reject(Func<Task> action, string label)
{
    try
    {
        await action();
        throw new Exception("Accepted " + label);
    }
    catch (IOException)
    {
        Console.WriteLine("PASS " + label);
    }
}

var handler = new Handler();
using var http = new HttpClient(handler);
var service = new ContentService(http);
ContentVersion Version(string p, string id)
{
    var bytes = Encoding.UTF8.GetBytes(id);
    handler.Bytes["/" + id] = bytes;
    return new()
    {
        ProjectId = p,
        Id = id,
        Name = p,
        VersionNumber = id,
        VersionType = "release",
        Files = [new()
        {
            Filename = p + ".jar",
            Size = bytes.Length,
            Url = "https://cdn.modrinth.com/" + id,
            Hashes = new()
            {
                ["sha512"] = Convert.ToHexString(SHA512.HashData(bytes))
            }
        }

        ]
    };
}

InstallPlan Plan(params ContentVersion[] v) => new(v.ToList(), v.ToDictionary(x => x.ProjectId, _ => "mod"));
var a1 = Version("a", "a1");
var a2 = Version("a", "a2");
var b1 = Version("b", "b1");
var b2 = Version("b", "b2");
var directory = Path.Combine(root, "content");
await service.Install(Plan(a1, b1), directory);
var provider = new Provider();
provider.Plans[a2.Id] = Plan(a2);
provider.Plans[b2.Id] = Plan(b2);
var preview = await ContentUpdates.Preview(provider, directory, [a2, b2], "1.21.1", "fabric", default);
Check(preview.Changes.Count == 2 && preview.Bytes == 4, "batch preview includes files and bytes");
var request = JsonSerializer.Deserialize<DownloadRequest>(JsonSerializer.Serialize(new DownloadRequest { PreparedPlan = preview.Plan, ContentFingerprint = preview.Fingerprint }))!;
Check(request.PreparedPlan!.Versions.Select(x => x.Id).SequenceEqual(new[] { "a2", "b2" }) && request.ContentFingerprint == preview.Fingerprint, "queue serializes exact approved plan");
await service.Install(request.PreparedPlan, directory, expectedFingerprint: request.ContentFingerprint);
Check(File.ReadAllText(Path.Combine(directory, "mods", "a.jar")) == "a2", "approved batch installed");
ContentTransaction.RestoreLatest(directory);
Check(File.ReadAllText(Path.Combine(directory, "mods", "a.jar")) == "a1" && ContentService.ReadManaged(directory).Single(x => x.ProjectId == "a").VersionId == "a1", "one-click rollback restores bytes and manifest");
var stale = await ContentUpdates.Preview(provider, directory, [a2], "1.21.1", "fabric", default);
File.WriteAllText(Path.Combine(directory, "mods", "extra.jar"), "manual");
var requests = handler.Requests;
await Reject(() => service.Install(stale.Plan, directory, expectedFingerprint: stale.Fingerprint), "stale preview rejected before download");
Check(requests == handler.Requests && File.ReadAllText(Path.Combine(directory, "mods", "extra.jar")) == "manual", "stale plan preserves manual addition");
File.Delete(Path.Combine(directory, "mods", "extra.jar"));
await service.Install(Plan(a2), directory);
File.WriteAllText(Path.Combine(directory, "mods", "a.jar"), "user-edit");
await Reject(() =>
{
    ContentTransaction.RestoreLatest(directory);
    return Task.CompletedTask;
}, "rollback refuses to overwrite edits");
provider.Plans[b2.Id] = Plan(a1, b2);
await Reject(() => ContentUpdates.Preview(provider, directory, [a2, b2], "1.21.1", "fabric", default), "batch dependency disagreement blocked");
string Archive(string name, Dictionary<string, string> entries)
{
    var path = Path.Combine(root, name + ".zip");
    using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
    using (var writer = new StreamWriter(archive.CreateEntry("mmc-pack.json").Open()))
        writer.Write("{\"formatVersion\":1,\"components\":[{\"uid\":\"net.minecraft\",\"version\":\"1.21.1\"}]}");
    foreach (var entry in entries)
    {
        using var writer = new StreamWriter(archive.CreateEntry("minecraft/" + entry.Key).Open());
        writer.Write(entry.Value);
    }

    return path;
}

var gameRoot = Path.Combine(root, "game");
var oldZip = Archive("old", new()
{
    ["mods/old.jar"] = "pack-old",
    ["config/test.cfg"] = "old-default"
});
var parallelArchive = Path.Combine(root, "parallel.mrpack");
var payloads = new[] {
    "parallel-a-" + Guid.NewGuid(),
    "parallel-b-" + Guid.NewGuid(),
    "parallel-c-" + Guid.NewGuid(),
    "parallel-d-" + Guid.NewGuid()
};
var entries = new List<object>();
for (var i = 0; i < 5; i++)
{
    var bytes = Encoding.UTF8.GetBytes(payloads[i % 4]);
    handler.Bytes["/parallel-" + i] = bytes;
    entries.Add(new
    {
        path = "mods/parallel-" + i + ".jar",
        fileSize = bytes.Length,
        hashes = new
        {
            sha512 = Convert.ToHexString(SHA512.HashData(bytes))
        },
        downloads = new[] { "https://cdn.modrinth.com/parallel-" + i }
    });
}

using (var zip = ZipFile.Open(parallelArchive, ZipArchiveMode.Create))
using (var writer = new StreamWriter(zip.CreateEntry("modrinth.index.json").Open()))
    writer.Write(JsonSerializer.Serialize(new
    {
        formatVersion = 1,
        game = "minecraft",
        name = "parallel",
        dependencies = new
        {
            minecraft = "1.21.1"
        },
        files = entries
    }));
var parallelPack = await new PackService(http).Import(parallelArchive, gameRoot, _ => Task.FromResult("base"), false);
Check(handler.Peak >= 2 && handler.Peak <= 4, "pack downloads use bounded parallel workers");
var parallelDirectory = Path.Combine(gameRoot, "instances", parallelPack.InstanceId);
Check(File.ReadAllText(Path.Combine(parallelDirectory, "mods", "parallel-0.jar")) == payloads[0] && File.ReadAllText(Path.Combine(parallelDirectory, "mods", "parallel-4.jar")) == payloads[0], "same-hash pack entries share cache without contention");
var newZip = Archive("new", new()
{
    ["mods/new.jar"] = "pack-new",
    ["config/test.cfg"] = "new-default"
});
var oldPack = await new PackService(http).Import(oldZip, gameRoot, _ => Task.FromResult("base"), false);
var catalog = new InstanceCatalog(gameRoot);
var oldDir = catalog.DirectoryFor(oldPack.InstanceId);
catalog.GetOrCreate(oldPack.InstanceId);
File.WriteAllText(Path.Combine(oldDir, "mods", "custom.jar"), "player-mod");
File.WriteAllText(Path.Combine(oldDir, "config", "test.cfg"), "player-config");
await service.Install(Plan(Version("custom-managed", "managed-v1")), oldDir);
Directory.CreateDirectory(Path.Combine(oldDir, "saves", "world"));
File.WriteAllText(Path.Combine(oldDir, "saves", "world", "level.dat"), "world-fixture");
using (var upgrade = await PackUpgrade.Prepare(http, gameRoot, oldPack.InstanceId, oldDir, newZip, _ => Task.FromResult("base"), false, null, default))
{
    Check(upgrade.HasBaseline && upgrade.Files.Any(f => f.Relative == "config/test.cfg" && f.Conflict), "pack upgrade identifies customized config conflict");
    var key = await PackUpgrade.Commit(upgrade, upgrade.Files.Where(f => f.Relative != "config/test.cfg").Select(f => f.Relative), new(), null, default);
    var result = catalog.DirectoryFor(key);
    Check(File.ReadAllText(Path.Combine(result, "config", "test.cfg")) == "new-default", "conflict choice uses new package config");
    Check(!File.Exists(Path.Combine(result, "mods", "old.jar")) && File.ReadAllText(Path.Combine(result, "mods", "custom.jar")) == "player-mod" && File.Exists(Path.Combine(result, "mods", "new.jar")), "obsolete pack mod removed and custom mod preserved");
    Check(File.ReadAllText(Path.Combine(result, "saves", "world", "level.dat")) == "world-fixture" && File.ReadAllText(Path.Combine(oldDir, "config", "test.cfg")) == "player-config", "world copied and original instance untouched");
    Check(ContentService.ReadManaged(result).Any(m => m.ProjectId == "custom-managed" && m.VersionId == "managed-v1"), "migrated custom mod retains accurate update ownership");
    Check(AtomicFile.ReadJson<Dictionary<string, string>>(Path.Combine(result, ".blocklaunch-upgrade.json"))["OriginalKey"] == oldPack.InstanceId, "upgrade records original instance for return");
}

using (var upgrade = await PackUpgrade.Prepare(http, gameRoot, oldPack.InstanceId, oldDir, newZip, _ => Task.FromResult("base"), false, null, default))
{
    File.WriteAllText(Path.Combine(oldDir, "mods", "custom.jar"), "changed-after-preview");
    await Reject(() => PackUpgrade.Commit(upgrade, upgrade.Files.Select(f => f.Relative), new(), null, default), "pack source changed after preview blocks commit");
}

using (var locked = new FileStream(Path.Combine(oldDir, "saves", "world", "session.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
    await Reject(() => PackUpgrade.Prepare(http, gameRoot, oldPack.InstanceId, oldDir, newZip, _ => Task.FromResult("base"), false, null, default), "externally running world blocks upgrade");
var networkHandler = new Handler();
using var network = new HttpClient(new NetworkPolicyHandler(new(), networkHandler));
networkHandler.Statuses.Enqueue(HttpStatusCode.ServiceUnavailable);
networkHandler.Statuses.Enqueue(HttpStatusCode.OK);
using var response = await network.GetAsync("https://libraries.minecraft.net/test.jar");
Check(networkHandler.Hosts.SequenceEqual(new[] { "libraries.minecraft.net", "bmclapi2.bangbang93.com" }), "network official failure switches to BMCLAPI");
Check(NetworkPolicyHandler.Mirror(new("https://login.microsoftonline.com/token")) == null && NetworkPolicyHandler.Mirror(new("https://api.modrinth.com/v2/search")) == null, "authentication and content APIs never mirrored");
Check(NetworkPolicyHandler.Mirror(new("https://libraries.minecraft.net.evil.example/test")) == null, "mirror matching rejects lookalike domains");
var authHandler = new Handler();
authHandler.Statuses.Enqueue(HttpStatusCode.ServiceUnavailable);
using (var client = new HttpClient(new NetworkPolicyHandler(new(), authHandler)))
using (var requestAuth = new HttpRequestMessage(HttpMethod.Get, "https://libraries.minecraft.net/protected"))
{
    requestAuth.Headers.Authorization = new("Bearer", "synthetic-token");
    using var authResponse = await client.SendAsync(requestAuth);
    Check(authHandler.Hosts.Count == 1 && authHandler.Hosts[0] == "libraries.minecraft.net", "authorized request never forwarded to mirror");
}

var preferredHandler = new Handler();
preferredHandler.Statuses.Enqueue(HttpStatusCode.NotFound);
preferredHandler.Statuses.Enqueue(HttpStatusCode.OK);
using (var client = new HttpClient(new NetworkPolicyHandler(new()
{
    DownloadSource = "BMCLAPI 优先"
}, preferredHandler)))
{
    using var preferredResponse = await client.GetAsync("https://libraries.minecraft.net/test.jar");
    Check(preferredHandler.Hosts.SequenceEqual(new[] { "bmclapi2.bangbang93.com", "libraries.minecraft.net" }), "mirror missing file falls back to official");
}

var recoveryRoot = Path.Combine(root, "interrupted-upgrade");
var orphanAlias = "pack-123456abcdef";
var stage = Path.Combine(recoveryRoot, ".blocklaunch-staging", "upgrade-fixture");
Directory.CreateDirectory(stage);
var orphan = Path.Combine(recoveryRoot, "instances", orphanAlias);
Directory.CreateDirectory(orphan);
AtomicFile.WriteJson(Path.Combine(orphan, ".blocklaunch-pack.json"), new InstalledPack("fixture", "1.21.1", "vanilla", "", "base", orphanAlias));
AtomicFile.WriteJson(Path.Combine(stage, "pack-operation.json"), new PackCommitRecord(orphanAlias, "Preparing"));
InstallationRecovery.Recover(recoveryRoot);
Check(!Directory.Exists(orphan) && Directory.Exists(Path.Combine(stage, "recovered-instance")), "interrupted upgrade candidate isolated without deletion");
try
{
    new NetworkSettings
    {
        ProxyMode = "自定义代理",
        ProxyAddress = "http://user:password@localhost:7890"
    }.Validate();
    throw new Exception("Credentials accepted");
}
catch (IOException)
{
    Console.WriteLine("PASS proxy credentials rejected");
}

Console.WriteLine("ALL_UPGRADE_CHECKS_PASSED");
sealed class Provider : IResourceProvider
{
    /// <summary>
    /// 获取或设置 Plans。
    /// </summary>
    public Dictionary<string, InstallPlan> Plans { get; } = new();

    /// <summary>
    /// 解析。
    /// </summary>
    public Task<InstallPlan> Resolve(ContentVersion version, string game, string loader) => Task.FromResult(Plans[version.Id]);
    /// <summary>
    /// 执行 Versions。
    /// </summary>
    public Task<List<ContentVersion>> Versions(string project, string game, string loader, string type) => Task.FromResult(Plans.Values.SelectMany(p => p.Versions).Where(v => v.ProjectId == project).ToList());
    /// <summary>
    /// 搜索。
    /// </summary>
    public Task<SearchPage> Search(string query, string game, string loader, string type, int offset = 0) => throw new NotSupportedException();
}

sealed class Handler : HttpMessageHandler
{
    /// <summary>
    /// 获取或设置 Bytes。
    /// </summary>
    public Dictionary<string, byte[]> Bytes { get; } = new();
    /// <summary>
    /// 获取或设置 Statuses。
    /// </summary>
    public Queue<HttpStatusCode> Statuses { get; } = new();
    /// <summary>
    /// 获取或设置 Hosts。
    /// </summary>
    public List<string> Hosts { get; } = [];

    int requests, active;

    public int Requests => requests;

    public int Peak
    {
        get;
        private set;
    }

    /// <summary>
    /// 发送。
    /// </summary>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        Interlocked.Increment(ref requests);
        var count = Interlocked.Increment(ref active);
        lock (Hosts)
        {
            Hosts.Add(request.RequestUri!.Host);
            Peak = Math.Max(Peak, count);
        }

        try
        {
            await Task.Delay(40, token);
            return new HttpResponseMessage(Statuses.Count > 0 ? Statuses.Dequeue() : HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Bytes.GetValueOrDefault(request.RequestUri!.AbsolutePath) ?? [])
            };
        }
        finally
        {
            Interlocked.Decrement(ref active);
        }
    }
}
