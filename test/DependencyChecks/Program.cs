using BlockLaunch;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var root = Path.Combine(Path.GetTempPath(), "BlockLaunch-dependencies-" + Guid.NewGuid().ToString("N"));
var handler = new FixtureHandler();
using var http = new HttpClient(handler);
var service = new ContentService(http);
void Check(bool condition, string label)
{
    if (!condition)
        throw new Exception(label);
    Console.WriteLine("PASS " + label);
}

ContentVersion V(string project, string id, params ContentDependency[] deps)
{
    var bytes = Encoding.UTF8.GetBytes(id);
    handler.Bytes["/" + id] = bytes;
    return new()
    {
        ProjectId = project,
        Id = id,
        Name = project,
        GameVersions = ["1.21.1"],
        Loaders = ["fabric"],
        Dependencies = deps.ToList(),
        Files = [new()
        {
            Filename = project + ".jar",
            Url = "https://cdn.modrinth.com/" + id,
            Size = bytes.Length,
            Hashes = new()
            {
                ["sha512"] = Convert.ToHexString(SHA512.HashData(bytes))
            }
        }

        ]
    };
}

ContentDependency Required(string p, string? v = null) => new()
{
    DependencyType = "required",
    ProjectId = p,
    VersionId = v
};
InstallPlan Plan(params ContentVersion[] vs) => new(vs.ToList(), vs.ToDictionary(v => v.ProjectId, _ => "mod"));
async Task Reject(Func<Task> action, string label)
{
    try
    {
        await action();
        throw new Exception("Accepted: " + label);
    }
    catch (IOException)
    {
        Console.WriteLine("PASS " + label);
    }
}

var dep = V("dependency", "dep-v1");
await service.Install(Plan(dep, V("empty-version", "ev1", Required("dependency", ""))), Path.Combine(root, "empty-version"));
Console.WriteLine("PASS empty optional version identity treated as project-only dependency");
await service.Install(Plan(dep, V("version-only", "vo1", Required("", dep.Id))), Path.Combine(root, "version-only"));
Console.WriteLine("PASS empty optional project identity treated as version-only dependency");
var consumer = V("consumer", "consumer-v1", Required("dependency", dep.Id));
var instance = Path.Combine(root, "managed");
await service.Install(Plan(dep, consumer), instance);
var depItem = ContentService.ListLocal(instance).Single(x => x.Managed?.ProjectId == "dependency");
await Reject(() =>
{
    ContentService.Toggle(depItem);
    return Task.CompletedTask;
}, "cannot disable required dependency");
await Reject(() =>
{
    ContentService.MoveToBackup(instance, depItem);
    return Task.CompletedTask;
}, "cannot remove required dependency");
Check(File.Exists(depItem.Path) && ContentService.ReadManaged(instance).Count == 2, "blocked operations preserve files and manifest");
var incompatibleUpdate = V("dependency", "dep-incompatible");
var requestsBeforeUpdate = handler.Requests;
await Reject(() => service.Install(Plan(incompatibleUpdate), instance), "dependency update cannot break active fixed-version consumer");
Check(handler.Requests == requestsBeforeUpdate && ContentService.ReadManaged(instance).Single(x => x.ProjectId == "dependency").VersionId == dep.Id, "rejected dependency update makes no download or metadata change");
ContentService.Toggle(ContentService.ListLocal(instance).Single(x => x.Managed?.ProjectId == "consumer"));
ContentService.Toggle(depItem);
Check(ContentService.ListLocal(instance).All(x => x.State == "已禁用"), "dependent can be disabled before dependency");
await Reject(() =>
{
    ContentService.Toggle(ContentService.ListLocal(instance).Single(x => x.Managed?.ProjectId == "consumer"));
    return Task.CompletedTask;
}, "cannot enable consumer while dependency disabled");
var depNew = V("dependency", "dep-v2");
await service.Install(Plan(depNew), instance);
Check(ContentService.ListLocal(instance).Single(x => x.Managed?.ProjectId == "dependency").State == "已禁用", "disabled consumer does not block dependency version update");
ContentService.Toggle(ContentService.ListLocal(instance).Single(x => x.Managed?.ProjectId == "dependency"));
await Reject(() =>
{
    ContentService.Toggle(ContentService.ListLocal(instance).Single(x => x.Managed?.ProjectId == "consumer"));
    return Task.CompletedTask;
}, "enabling rejects mismatched fixed dependency version");
var consumerNew = V("consumer", "consumer-v2", Required("dependency", depNew.Id));
await service.Install(Plan(consumerNew), instance);
ContentService.Toggle(ContentService.ListLocal(instance).Single(x => x.Managed?.ProjectId == "consumer"));
Check(ContentService.ListLocal(instance).All(x => x.State == "已启用"), "compatible update allows enable");
var conflict = V("conflict", "conflict-v1", new ContentDependency { DependencyType = "incompatible", ProjectId = "dependency" });
await Reject(() => service.Install(Plan(conflict), instance), "active incompatible installation rejected");
ContentService.Toggle(ContentService.ListLocal(instance).Single(x => x.Managed?.ProjectId == "consumer"));
ContentService.MoveToBackup(instance, ContentService.ListLocal(instance).Single(x => x.Managed?.ProjectId == "dependency"));
Check(ContentService.ReadManaged(instance).Count == 1, "disabled consumer does not block removal");
var conflictRoot = Path.Combine(root, "disabled-conflict");
await service.Install(Plan(conflict), conflictRoot);
ContentService.Toggle(ContentService.ListLocal(conflictRoot).Single());
await service.Install(Plan(dep), conflictRoot);
Check(File.Exists(Path.Combine(conflictRoot, "mods", "dependency.jar")), "disabled incompatible content does not block installation");
await Reject(() =>
{
    ContentService.Toggle(ContentService.ListLocal(conflictRoot).Single(x => x.Managed?.ProjectId == "conflict"));
    return Task.CompletedTask;
}, "enabling disabled incompatible content is rejected");
// Modrinth metadata fixtures: no live accounts or downloads.
void Project(string p) => handler.Json["/v2/project/" + p] = "{\"project_type\":\"mod\",\"client_side\":\"required\"}";
var opts = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
};
void Remote(ContentVersion value)
{
    Project(value.ProjectId);
    handler.Json["/v2/version/" + value.Id] = JsonSerializer.Serialize(value, opts);
}

var b = V("b", "b-old");
var c = V("c", "c-v1", Required("b"));
var a = V("a", "a-v1", Required("b", b.Id), Required("c", c.Id));
Project("a");
Remote(b);
Remote(c);
var resolved = await service.Resolve(a, "1.21.1", "fabric");
Check(resolved.Versions.Count == 3 && resolved.Versions.Single(v => v.ProjectId == "b").Id == "b-old", "project-only dependency reuses already selected pinned version");
var orderRoot = V("a", "ordered", Required("b"), Required("b", b.Id));
var ordered = await service.Resolve(orderRoot, "1.21.1", "fabric");
Check(ordered.Versions.Count == 2 && ordered.Versions.Single(v => v.ProjectId == "b").Id == b.Id, "fixed dependencies take priority over project-only declarations");
handler.Json["/v2/version/b-old"] = JsonSerializer.Serialize(V("unexpected", "b-old"), opts);
await Reject(() => service.Resolve(a, "1.21.1", "fabric"), "version response project mismatch rejected");
handler.Json["/v2/version/b-old"] = JsonSerializer.Serialize(V("b", "wrong-id"), opts);
await Reject(() => service.Resolve(a, "1.21.1", "fabric"), "version response ID mismatch rejected");
await Reject(() => service.Resolve(new(), "1.21.1", "fabric"), "missing root identity rejected");
using var cts = new CancellationTokenSource();
cts.Cancel();
var requests = handler.Requests;
try
{
    await new ContentService(http, cts.Token).Resolve(a, "1.21.1", "fabric");
    throw new Exception("Cancellation ignored");
}
catch (OperationCanceledException)
{
}

Check(handler.Requests == requests, "cancelled dependency resolution makes no requests");
Console.WriteLine("ALL_DEPENDENCY_CHECKS_PASSED");
sealed class FixtureHandler : HttpMessageHandler
{
    /// <summary>
    /// 获取或设置 Bytes。
    /// </summary>
    public Dictionary<string, byte[]> Bytes { get; } = new();
    /// <summary>
    /// 获取或设置 Json。
    /// </summary>
    public Dictionary<string, string> Json { get; } = new();
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
        var path = request.RequestUri!.AbsolutePath;
        if (request.RequestUri.Host == "cdn.modrinth.com")
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Bytes[path]) });
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Json[path], Encoding.UTF8, "application/json") });
    }
}
