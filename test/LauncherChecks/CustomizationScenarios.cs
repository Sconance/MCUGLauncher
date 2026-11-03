using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BlockLaunch;
using MCUGLauncher.Models;
using MCUGLauncher.Services;
using MCUGLauncher.UseCases;

internal static class CustomizationScenarios
{
    static JsonElement Message(object value) => JsonSerializer.SerializeToElement(value);
    static void Check(bool value, string name)
    {
        if (!value)
            throw new Exception(name);
        Console.WriteLine("PASS " + name);
    }
    public static async Task Run(string fixture)
    {
        var root = Path.Combine(fixture, "custom-game");
        var version = Path.Combine(root, "versions", "1.21.1");
        Directory.CreateDirectory(version);
        File.WriteAllText(Path.Combine(version, "1.21.1.json"), """{"id":"1.21.1","type":"release","libraries":[{"name":"net.fabricmc:fabric-loader:0.16.0"}]}""");
        var store = new ConfigStore(Path.Combine(fixture, "custom-config"));
        store.Save(new AppConfig { Root = root, LastInstance = "1.21.1", LastVersion = "1.21.1" });
        var messages = new List<JsonElement>();
        using var context = new LauncherContext(value => messages.Add(Message(value)), store);
        var sourceSettings = new SettingsCommands(context);
        var originalSource = context.Config.DataSources.Announcements;
        sourceSettings.SaveCustomSources(Message(new
        {
            dataSources = new
            {
                announcements = "https://example.org/custom.json",
                servers = "",
                recommendations = "",
                quotes = ""
            }
        }));
        Check(context.Config.DataSources.Announcements == originalSource && context.Config.EffectiveDataSourceMode == "default", "saving custom sources does not activate them");
        sourceSettings.UseDataSources("custom");
        Check(context.Config.DataSources.Announcements == "https://example.org/custom.json", "using custom sources activates the saved preset");
        sourceSettings.SaveCustomSources(Message(new
        {
            dataSources = new
            {
                announcements = "https://example.org/edited.json",
                servers = "",
                recommendations = "",
                quotes = ""
            }
        }));
        Check(context.Config.DataSources.Announcements == "https://example.org/custom.json", "saving an active custom preset does not change live sources");
        sourceSettings.UseDataSources("default");
        Check(context.Config.DataSources.Announcements == originalSource && context.Config.CustomDataSources?.Announcements == "https://example.org/edited.json", "default selection preserves custom addresses");
        var settings = new LaunchOptionsCommands(context);
        object Options(bool auto, int ram) => new
        {
            autoMemory = auto,
            ramMb = ram,
            java = "fixture-java",
            width = 1000,
            height = 700,
            fullScreen = false,
            jvmArguments = "-Dfixture=true",
            windowTitle = "Custom title",
            server = "localhost:25565"
        };
        var launchOptions = settings.Read(Message(new
        {
            version = "",
            requestId = "global"
        }));
        var token = launchOptions.Token;
        var saveResult = settings.Save(Message(new
        {
            token,
            name = "",
            inherit = false,
            settings = Options(true, 2048)
        }));
        Check(store.Load().AutoMemory && InstanceSnapshot.Capture(context.Config, "1.21.1").Settings.AutoMemory, "global automatic memory is saved and used by launch snapshot");
        launchOptions = settings.Read(Message(new
        {
            version = "1.21.1",
            requestId = "instance"
        }));
        token = launchOptions.Token;
        var other = Path.Combine(fixture, "custom-other");
        Directory.CreateDirectory(other);
        context.Config.Root = other;
        saveResult = settings.Save(Message(new
        {
            token,
            name = "My instance",
            inherit = false,
            settings = Options(false, 1536)
        }));
        var descriptor = new InstanceCatalog(root).GetOrCreate("1.21.1");
        Check(descriptor.DisplayName == "My instance" && descriptor.Launch!.RamMb == 1536 && descriptor.Launch.JvmArguments == "-Dfixture=true", "instance options retain original directory and custom name");
        context.Config.Root = root;
        launchOptions = settings.Read(Message(new
        {
            version = "1.21.1",
            requestId = "inherit"
        }));
        token = launchOptions.Token;
        saveResult = settings.Save(Message(new
        {
            token,
            name = "My instance",
            inherit = true,
            settings = Options(false, 1536)
        }));
        Check(new InstanceCatalog(root).GetOrCreate("1.21.1").Launch == null, "instance can return to global inheritance");

        using var http = new HttpClient(new VersionHandler());
        var queued = new List<(string Root, string Target, DownloadRequest Request)>();
        void Queue(string name, string gameRoot, string target, DownloadRequest request) => queued.Add((gameRoot, target, request));
        var resources = new ResourceChoiceCommands(context, http, Queue);
        var resourcesResult = await resources.ListAsync(Message(new
        {
            id = "fixture",
            category = "mod",
            instance = "1.21.1",
            requestId = "resource"
        }));
        token = resourcesResult.Result!.Token;
        context.Config.Root = other;
        try
        {
            resources.Queue(Message(new
            {
                token,
                versionId = "unlisted",
                mode = "instance",
                path = ""
            }));
            throw new Exception("unlisted version accepted");
        }
        catch (ArgumentException) { }
        resources.Queue(Message(new
        {
            token,
            versionId = "v2",
            mode = "instance",
            path = ""
        }));
        Check(queued.Last().Root == root && queued.Last().Request.Content!.Id == "v2", "resource confirmation uses explicit version and frozen root");
        context.Config.Root = root;
        var archive = Path.Combine(fixture, "custom-pack.mrpack");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        using (var writer = new StreamWriter(zip.CreateEntry("modrinth.index.json").Open()))
            writer.Write("""{"formatVersion":1,"game":"minecraft","name":"Pack default","dependencies":{"minecraft":"1.21.1"},"files":[]}""");
        var imports = new InstallationCommands(context, Queue);
        var before = queued.Count;
        var packResult = await imports.PreviewPackAsync(Message(new
        {
            path = archive
        }));
        token = packResult.Preview!.Token;
        Check(queued.Count == before, "pack selection previews metadata without installing");
        imports.ConfirmPack(Message(new
        {
            token,
            name = "Chosen-pack-name",
            directory = other
        }));
        Check(queued.Last().Root == other && queued.Last().Request.PackInstance == "Chosen-pack-name", "pack confirmation honors custom instance name and directory");

        var roles = new MuaProfileSelection(context);
        var selection = roles.ChooseAsync(new[] { new MuaProfile("first", "One"), new MuaProfile("second", "Two") });
        token = messages.Last().GetProperty("token").GetString();
        Check(!selection.IsCompleted, "multiple MUA roles wait for explicit choice");
        roles.Select(Message(new
        {
            token,
            profileId = "second"
        }));
        Check((await selection)!.Name == "Two", "MUA selection uses chosen role rather than first role");
        var cancelled = roles.ChooseAsync(new[] { new MuaProfile("first", "One"), new MuaProfile("second", "Two") });
        token = messages.Last().GetProperty("token").GetString();
        roles.Select(Message(new
        {
            token,
            profileId = ""
        }));
        Check(await cancelled == null, "cancelling role selection does not choose a default role");
        context.Config.Root = root;
        context.Config.AutoMemory = true;
        context.Config.Ram = 65000;
        LaunchRequest? captured = null;
        var game = new GameCommands(context, (request, _) =>
        {
            captured = request;
            return Task.FromResult(new Process { StartInfo = new ProcessStartInfo("cmd.exe") { Arguments = "/c exit 0", UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true } });
        });
        await game.LaunchAsync("1.21.1");
        Check(captured?.Settings?.AutoMemory == true && captured.Ram >= 1024 && captured.Ram <= 8192, "automatic memory affects the actual launch request instead of using the manual value");

    }

    sealed class VersionHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var versions = new[] { "v1", "v2" }.Select(id => new
            {
                id,
                project_id = "fixture",
                name = id,
                game_versions = new[] { "1.21.1" },
                loaders = new[] { "fabric" },
                files = new[] { new { filename = "fixture.jar", url = "https://cdn.modrinth.com/fixture.jar", size = 1, primary = true, hashes = new { sha512 = Convert.ToHexString(SHA512.HashData(new byte[] { 1 })) } } }
            });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(versions), Encoding.UTF8, "application/json") });
        }
    }
}
