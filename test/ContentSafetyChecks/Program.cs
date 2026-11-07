using BlockLaunch;
using System.Net;
using System.Security.Cryptography;
using System.Text;

var root = Path.Combine(Path.GetTempPath(), "BlockLaunch-content-safety-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
void Check(bool ok, string label)
{
    if (!ok)
        throw new Exception(label);
    Console.WriteLine("PASS " + label);
}

var handler = new PayloadHandler();
using var http = new HttpClient(handler);
var service = new ContentService(http);
ContentVersion Version(string project, string id, string file, string body)
{
    var bytes = Encoding.UTF8.GetBytes(body);
    var url = "https://cdn.modrinth.com/" + project + "/" + id;
    handler.Payloads[url] = bytes;
    return new()
    {
        ProjectId = project,
        Id = id,
        Name = id,
        Files = [new()
        {
            Filename = file,
            Url = url,
            Size = bytes.Length,
            Hashes = new()
            {
                ["sha512"] = Convert.ToHexString(SHA512.HashData(bytes))
            }
        }

        ]
    };
}

InstallPlan Plan(ContentVersion v, string kind = "mod") => new([v], new()
{
    [v.ProjectId] = kind
});
async Task Rejected(InstallPlan plan, string label)
{
    var path = Path.Combine(root, Guid.NewGuid().ToString("N"));
    var count = handler.Requests;
    try
    {
        await service.Install(plan, path);
        throw new Exception("Accepted " + label);
    }
    catch (IOException)
    {
    }

    Check(!Directory.Exists(path) && handler.Requests == count, label + " before filesystem/network");
}

await Rejected(new([], new()), "empty plan rejected");
var v = Version("one", "v1", "one.jar", "one");
await Rejected(new([v, v], new()
{
    ["one"] = "mod"
}), "duplicate project rejected");
await Rejected(new([v], new()), "missing type rejected");
await Rejected(Plan(v, "unknown"), "unknown type rejected");
var bad = Version("bad", "v1", "bad.jar", "bad");
bad.Files[0].Url = "https://example.com/bad.jar";
await Rejected(new([v, bad], new()
{
    ["one"] = "mod",
    ["bad"] = "mod"
}), "later invalid URL rejects entire plan");
bad.Files[0].Url = "https://cdn.modrinth.com/bad/v1";
bad.Files[0].Hashes["sha512"] = "invalid";
await Rejected(Plan(bad), "invalid hash rejected");
var wrong = Version("wrong", "v1", "wrong.zip", "wrong");
await Rejected(Plan(wrong), "wrong extension rejected");
wrong.Files[0].Filename = "wrong.jar";
wrong.Files[0].Size = -1;
await Rejected(Plan(wrong), "negative size rejected");
using (var cts = new CancellationTokenSource())
{
    cts.Cancel();
    var path = Path.Combine(root, "cancelled");
    try
    {
        await new ContentService(http, cts.Token).Install(Plan(v), path);
        throw new Exception("Cancellation ignored");
    }
    catch (OperationCanceledException)
    {
    }

    try
    {
        await new ContentService(http, cts.Token).ImportFiles(path, "mods", []);
        throw new Exception("Import cancellation ignored");
    }
    catch (OperationCanceledException)
    {
    }

    Check(!Directory.Exists(path), "pre-cancelled install/import leave no directory");
}

var sharedName = Path.Combine(root, "same-name");
var resource = Version("resource", "r1", "same.zip", "resource-bytes");
var shader = Version("shader", "s1", "same.zip", "shader-bytes");
await service.Install(new([resource, shader], new()
{
    ["resource"] = "resourcepack",
    ["shader"] = "shader"
}), sharedName);
Check(File.ReadAllText(Path.Combine(sharedName, "resourcepacks", "same.zip")) == "resource-bytes" && File.ReadAllText(Path.Combine(sharedName, "shaderpacks", "same.zip")) == "shader-bytes", "cross-category same filename preserves distinct payloads");
var importRoot = Path.Combine(root, "imports");
Directory.CreateDirectory(Path.Combine(importRoot, "mods"));
var imported = Path.Combine(root, "local.jar");
File.WriteAllText(imported, "new");
File.WriteAllText(Path.Combine(importRoot, "mods", "local.jar.disabled"), "keep-disabled");
Check(await service.ImportFiles(importRoot, "mods", [imported]) == 0 && !File.Exists(Path.Combine(importRoot, "mods", "local.jar")) && File.ReadAllText(Path.Combine(importRoot, "mods", "local.jar.disabled")) == "keep-disabled", "import skips disabled duplicate");
var stateRoot = Path.Combine(root, "state-change");
var original = Version("state", "old", "old.jar", "old-content");
await service.Install(Plan(original), stateRoot);
var newer = Version("state", "new", "new.jar", "new-content");
try
{
    await service.Install(Plan(newer), stateRoot, _ =>
    {
        var old = Path.Combine(stateRoot, "mods", "old.jar");
        if (File.Exists(old))
            File.Move(old, old + ".disabled");
    });
    throw new Exception("State change accepted");
}
catch (IOException ex)
{
    Check(ex.Message.Contains("启用状态"), "state change during download rejected");
}

Check(File.Exists(Path.Combine(stateRoot, "mods", "old.jar.disabled")) && !File.Exists(Path.Combine(stateRoot, "mods", "new.jar")) && ContentService.ReadManaged(stateRoot).Single().VersionId == "old", "external toggle preserved without manifest change");
File.Copy(Path.Combine(stateRoot, "mods", "old.jar.disabled"), Path.Combine(stateRoot, "mods", "old.jar"));
try
{
    ContentService.MoveToBackup(stateRoot, ContentService.ListLocal(stateRoot).First());
    throw new Exception("Ambiguous removal accepted");
}
catch (IOException)
{
}

Check(ContentService.ListLocal(stateRoot).Count == 2 && ContentService.ReadManaged(stateRoot).Count == 1, "ambiguous removal preserves files and ownership");
foreach (var change in new[] {
    "edit",
    "delete"
}

)
{
    var changeRoot = Path.Combine(root, "during-" + change);
    var oldVersion = Version(change, "old", "old.jar", "old-" + change);
    await service.Install(Plan(oldVersion), changeRoot);
    var newVersion = Version(change, "new", "new.jar", "new-" + change);
    var oldFile = Path.Combine(changeRoot, "mods", "old.jar");
    try
    {
        await service.Install(Plan(newVersion), changeRoot, _ =>
        {
            if (change == "edit")
                File.WriteAllText(oldFile, "user-change");
            else
                File.Delete(oldFile);
        });
        throw new Exception("Concurrent " + change + " accepted");
    }
    catch (IOException)
    {
    }

    Check(!File.Exists(Path.Combine(changeRoot, "mods", "new.jar")) && ContentService.ReadManaged(changeRoot).Single().VersionId == "old" && (change == "edit" ? File.ReadAllText(oldFile) == "user-change" : !File.Exists(oldFile)), "concurrent " + change + " preserved without replacement");
}

var blockedRoot = Path.Combine(root, "blocked");
Directory.CreateDirectory(Path.Combine(blockedRoot, "mods", "one.jar"));
var before = handler.Requests;
try
{
    await service.Install(Plan(v), blockedRoot);
    throw new Exception("Directory accepted");
}
catch (IOException)
{
}

Check(handler.Requests == before, "directory collision fails before downloading");
Console.WriteLine("ALL_CONTENT_SAFETY_CHECKS_PASSED");
sealed class PayloadHandler : HttpMessageHandler
{
    /// <summary>
    /// 获取或设置 Payloads。
    /// </summary>
    public Dictionary<string, byte[]> Payloads { get; } = new();
    public int Requests
    {
        get;
        private set;
    }

    /// <summary>
    /// 发送。
    /// </summary>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests++;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Payloads[request.RequestUri!.AbsoluteUri]) });
    }
}
