using System.IO;
using System.Net.Http;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using BlockLaunch;
using MCUGLauncher.Services;
using MCUGLauncher.UseCases;

internal static class ResourceScenarios
{
    public static async Task Run(string fixture)
    {
        var vanilla = ResourceCommands.ParseVanillaVersions(
            """{"versions":[{"id":"1.21.1","type":"release","releaseTime":"2024-06-13T00:00:00+00:00"},{"id":"24w20a","type":"snapshot","releaseTime":"2024-05-14T00:00:00+00:00"},{"id":"b1.7.3","type":"old_beta","releaseTime":"2011-07-08T00:00:00+00:00"},{"id":"in-20100618","type":"old_alpha","releaseTime":"2010-06-18T00:00:00+00:00"}]}""");
        if (vanilla.Count != 4 || vanilla.Select(item => item.Type).Distinct().Count() != 4)
            throw new Exception("vanilla catalog did not preserve release, snapshot, and old version types");
        Console.WriteLine("PASS Mojang catalog retains release, snapshot, and old version entries");

        using var handler = new ResourceFixtureHandler();
        using var http = new HttpClient(handler);
        var versions = await new ContentService(http).Versions("fixture", "1.21.1", "neoforge", "mod");
        if (versions.Count != 1 || !handler.Query.Contains("1.21.1") || !handler.Query.Contains("neoforge"))
            throw new Exception("Core filters not used");
        Console.WriteLine("PASS Core resource versions use requested game and loader");
        var executor = new DownloadExecutor(http);
        var record = new OperationRecord { Download = new DownloadRequest { Kind = "resource", Destination = Path.Combine(fixture, "verified"), ResourceKind = "mod", Content = versions[0] } };
        var coordinator = new OperationCoordinator(Path.Combine(fixture, "resource-operations"));
        await coordinator.Run("verified download", fixture, fixture, task => executor.ExecuteAsync(record, task));
        var path = ResourceDownloads.PathFor(record.Download.Destination, "mod", versions[0]);
        if (File.ReadAllText(path) != "verified bytes")
            throw new Exception("download bytes differ");
        Console.WriteLine("PASS task executor uses Core SHA-512 verified resource download");
        versions[0].Files[0].Filename = "../escaped.jar";
        try
        {
            ResourceDownloads.PathFor(fixture, "mod", versions[0]);
            throw new Exception("unsafe filename accepted");
        }
        catch (IOException) { Console.WriteLine("PASS Core rejects unsafe resource filenames"); }
        versions[0].Files.Clear();
        try
        {
            ResourceDownloads.PathFor(fixture, "mod", versions[0]);
            throw new Exception("empty files accepted");
        }
        catch (IOException) { Console.WriteLine("PASS Core rejects empty resource file lists"); }
    }
    sealed class ResourceFixtureHandler : HttpMessageHandler
    {
        public string Query = "";
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (request.RequestUri!.Host == "cdn.modrinth.com")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("verified bytes") });
            Query = Uri.UnescapeDataString(request.RequestUri.Query);
            var version = new
            {
                id = "version",
                project_id = "fixture",
                name = "Fixture",
                game_versions = new[] { "1.21.1" },
                loaders = new[] { "neoforge" },
                files = new[] { new { filename = "fixture.jar", url = "https://cdn.modrinth.com/data/fixture/versions/version/fixture.jar", size = 14, primary = true, hashes = new { sha512 = Convert.ToHexString(SHA512.HashData(Encoding.UTF8.GetBytes("verified bytes"))).ToLowerInvariant() } } }
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new[] { version }), Encoding.UTF8, "application/json") });
        }
    }
}
