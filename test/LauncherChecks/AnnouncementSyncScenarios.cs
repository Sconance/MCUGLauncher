using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MCUGLauncher.Services;

internal static class AnnouncementSyncScenarios
{
    public static async Task RunAsync(string fixture, Action<bool, string> check)
    {
        const string mirror = "https://gitee.com/Sconance/MCUGL-Config/raw/main/sources.json";
        const string root = "https://cdn.example.test/announcements/";
        const string manifest = root + "manifest.json";
        const string oldName = "2026-11-08-old.md";
        const string newName = "2026-11-09-new.md";
        var old = Encoding.UTF8.GetBytes("# preview_title\n旧公告");
        var revised = Encoding.UTF8.GetBytes("# preview_title\n更新公告");
        var latest = Encoding.UTF8.GetBytes("# preview_title\n新公告");
        var responses = new Dictionary<string, byte[]>
        {
            [mirror] = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                resources = new
                {
                    announcements = new
                    {
                        primary = root,
                        mirrors = Array.Empty<string>()
                    }
                }
            })),
            [manifest] = Manifest((oldName, old)),
            [root + oldName] = old
        };
        var requests = new List<string>();
        using var http = new HttpClient(new FixtureHandler(responses, requests));
        var cache = Path.Combine(fixture, "announcement-sync");
        var service = new CommunityService(http, cache);
        const string source = AnnouncementSyncService.Primary;

        var first = await service.ReadAnnouncementsAsync(source, CancellationToken.None);
        check(first.Items.Single().Title == "旧公告" && first.Error.Length == 0
            && service.LastAnnouncementSync?.Outcome == AnnouncementSyncOutcome.Updated
            && requests.Take(2).SequenceEqual([source, mirror]),
            "公告首次同步，GitHub 失败时使用 Gitee");

        requests.Clear();
        responses[manifest] = Manifest((oldName, old), (newName, latest));
        responses[root + newName] = latest;
        var second = await service.ReadAnnouncementsAsync(source, CancellationToken.None);
        check(second.Items.Select(item => item.Title).SequenceEqual(["新公告", "旧公告"])
            && second.Items[0].PublishedAt?.Day == 9
            && requests.Contains(root + newName) && !requests.Contains(root + oldName),
            "公告增量下载复用未变化的文件并按日期排序");

        requests.Clear();
        await service.ReadAnnouncementsAsync(source, CancellationToken.None);
        check(service.LastAnnouncementSync?.Outcome == AnnouncementSyncOutcome.Unchanged
            && !requests.Any(url => url.EndsWith(".md", StringComparison.Ordinal)),
            "清单内容未变时无需再次下载公告");

        responses[manifest] = Manifest((oldName, revised));
        var cached = await service.ReadAnnouncementsAsync(source, CancellationToken.None);
        check(cached.Items.Length == 2 && cached.Error.Length > 0
            && service.LastAnnouncementSync?.Outcome == AnnouncementSyncOutcome.Cached,
            "校验失败保留完整的上次成功快照");

        var savedIndex = responses[mirror];
        responses.Remove(mirror);
        var restarted = new CommunityService(http, cache);
        var offline = await restarted.ReadAnnouncementsAsync(source, CancellationToken.None);
        check(offline.Items.Length == 2 && restarted.LastAnnouncementSync?.LastSuccess is not null,
            "重启且离线时可以使用持久化快照");
        var foreign = await restarted.ReadAnnouncementsAsync("https://elsewhere.example/manifest.json", CancellationToken.None);
        check(foreign.Items.Length == 0 && restarted.LastAnnouncementSync?.Outcome == AnnouncementSyncOutcome.Failed,
            "不同公告源之间不共享缓存");
        responses[mirror] = savedIndex;

        responses[manifest] = Manifest(("../outside.md", revised));
        var invalid = await service.ReadAnnouncementsAsync(source, CancellationToken.None);
        check(invalid.Items.Length == 2 && invalid.Error.Length > 0,
            "拒绝带上级目录的公告文件路径");

        responses[manifest] = Manifest((oldName, revised));
        responses[root + oldName] = revised;
        var updated = await service.ReadAnnouncementsAsync(source, CancellationToken.None);
        check(updated.Items.Single().Title == "更新公告", "同步替换改动并移除清单中删除的公告");

        responses[manifest] = Manifest();
        var empty = await service.ReadAnnouncementsAsync(source, CancellationToken.None);
        var tested = await service.TestAnnouncementAsync(manifest, CancellationToken.None);
        check(empty.Items.Length == 0 && empty.Error.Length == 0 && tested.Ok,
            "空清单是有效同步结果且支持直接测试清单地址");
    }

    static byte[] Manifest(params (string Path, byte[] Bytes)[] entries) =>
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 1,
            files = entries.Select(entry => new
            {
                path = entry.Path,
                size = entry.Bytes.Length,
                sha256 = Convert.ToHexString(SHA256.HashData(entry.Bytes)).ToLowerInvariant()
            })
        });

    sealed class FixtureHandler(Dictionary<string, byte[]> responses, List<string> requests) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.AbsoluteUri;
            requests.Add(url);
            return Task.FromResult(responses.TryGetValue(url, out var bytes)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) }
                : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }
    }
}
