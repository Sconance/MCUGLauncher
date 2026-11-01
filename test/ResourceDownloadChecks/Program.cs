using BlockLaunch;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var root = Path.Combine(Path.GetTempPath(), "BlockLaunch-resource-check-" + Guid.NewGuid().ToString("N"));
var downloads = Path.Combine(root, "downloads");
var game = Path.Combine(root, "minecraft");
Directory.CreateDirectory(game);
var sentinel = Path.Combine(game, "player-data.txt");
File.WriteAllText(sentinel, "keep");
var bytes = Encoding.UTF8.GetBytes("verified resource bytes");
var hash = Convert.ToHexString(SHA512.HashData(bytes));
var handler = new Handler(bytes);
using var http = new HttpClient(handler);
ContentVersion V(string ext, string id = "version1") => new()
{
    ProjectId = "project1",
    Id = id,
    Files = [new()
    {
        Filename = "file" + ext,
        Size = bytes.Length,
        Url = "https://cdn.modrinth.com/file" + ext,
        Primary = true,
        Hashes = new()
        {
            ["sha512"] = hash
        }
    }

    ]
};
void Assert(bool value, string name)
{
    if (!value)
        throw new Exception(name);
    Console.WriteLine("PASS " + name);
}

foreach (var pair in new[] {
    ("mod", ".jar"),
    ("modpack", ".mrpack"),
    ("datapack", ".zip"),
    ("shader", ".zip"),
    ("resourcepack", ".zip")
}

)
{
    var path = await ResourceDownloads.Download(http, downloads, pair.Item1, V(pair.Item2));
    Assert(File.ReadAllBytes(path).SequenceEqual(bytes) && Path.GetFullPath(path).StartsWith(downloads + Path.DirectorySeparatorChar), "verified standalone " + pair.Item1);
}

Assert(Directory.GetFiles(game, "*", SearchOption.AllDirectories).SequenceEqual(new[] { sentinel }) && File.ReadAllText(sentinel) == "keep", "no game files or managed manifests created");
var before = handler.Calls;
await ResourceDownloads.Download(http, downloads, "mod", V(".jar"));
Assert(handler.Calls == before, "reuse hash-verified completed file");
var original = ResourceDownloads.PathFor(downloads, "mod", V(".jar"));
File.WriteAllText(original, "manually edited");
try
{
    await ResourceDownloads.Download(http, downloads, "mod", V(".jar"));
    throw new Exception("overwrote player file");
}
catch (IOException)
{
    Assert(File.ReadAllText(original) == "manually edited" && handler.Calls == before, "reject modified target without download");
}

foreach (var bad in new[] {
    V(".jar"),
    V(".jar"),
    V(".jar"),
    V(".zip")
}

)
{
    var index = before++;
    if (index == handler.Calls)
        bad.Files[0].Filename = "../escape.jar";
    else if (index == handler.Calls + 1)
        bad.Files[0].Url = "https://cdn.modrinth.com.evil.invalid/file.jar";
    else if (index == handler.Calls + 2)
        bad.Files[0].Hashes["sha512"] = "bad";
    try
    {
        ResourceDownloads.PathFor(downloads, "mod", bad);
        throw new Exception("accepted invalid resource");
    }
    catch (IOException)
    {
        Console.WriteLine("PASS reject invalid resource");
    }
}

using var cancellation = new CancellationTokenSource();
cancellation.Cancel();
var cancelledPath = Path.Combine(root, "cancelled");
try
{
    await ResourceDownloads.Download(http, cancelledPath, "mod", V(".jar"), token: cancellation.Token);
    throw new Exception("ignored cancellation");
}
catch (OperationCanceledException)
{
    Assert(!Directory.Exists(cancelledPath), "pre-cancelled job creates nothing");
}

var request = new DownloadRequest
{
    Kind = "resource",
    ResourceKind = "mod",
    Destination = downloads,
    Content = V(".jar")
};
var restored = JsonSerializer.Deserialize<DownloadRequest>(JsonSerializer.Serialize(request))!;
Assert(restored.Kind == "resource" && restored.Destination == downloads && restored.Content!.Id == "version1", "persistent queue retains download destination and exact resource");
Console.WriteLine("ALL_RESOURCE_DOWNLOAD_CHECKS_PASSED");
sealed class Handler(byte[] bytes) : HttpMessageHandler
{
    public int Calls;

    /// <summary>
    /// 发送。
    /// </summary>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        Calls++;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
    }
}
