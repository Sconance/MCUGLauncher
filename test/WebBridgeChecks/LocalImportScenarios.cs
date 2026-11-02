using System.Text.Json;
using System.IO.Compression;
using MCUGLauncher;
using MCUGLauncher.Models;
using MCUGLauncher.Services;
using MCUGLauncher.UseCases;

internal static class LocalImportScenarios
{
    static void Check(bool condition, string name)
    {
        if (!condition)
            throw new Exception(name);
        Console.WriteLine("PASS " + name);
    }
    public static async Task Run(string fixture)
    {
        var data = Path.Combine(fixture, "local-import");
        var root = Path.Combine(data, "game");
        foreach (var key in new[] { "1.21.1", "other" })
        {
            var directory = Path.Combine(root, "versions", key);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, key + ".json"), """{"id":"1.21.1","type":"release","libraries":[{"name":"net.fabricmc:fabric-loader:0.16.0"}]}""");
        }
        var store = new ConfigStore(data);
        store.Save(new AppConfig { Root = root, LastInstance = "1.21.1" });
        var messages = new List<JsonElement>();
        using var context = new LauncherContext(value => messages.Add(FixtureMessages.Capture(value)), store);
        var coordinator = new OperationCoordinator(Path.Combine(data, "execution"));
        var commands = new LocalImportCommands(context, (name, gameRoot, directory, request) => coordinator.Enqueue(name, gameRoot, directory, request));
        var file = Path.Combine(data, "fixture.jar");
        using (var zip = ZipFile.Open(file, ZipArchiveMode.Create))
        using (var writer = new StreamWriter(zip.CreateEntry("fabric.mod.json").Open()))
            writer.Write("{\"schemaVersion\":1,\"id\":\"fixture\",\"version\":\"1.0\"}");
        var original = File.ReadAllBytes(file);
        await commands.Preview(JsonSerializer.SerializeToElement(new
        {
            path = file
        }));
        var preview = messages.Last();
        Check(coordinator.History().Count == 0, "dropping local resource only previews before confirmation");
        var target = preview.GetProperty("target").GetString()!;
        context.Config.LastInstance = "other";
        commands.Confirm(JsonSerializer.SerializeToElement(new
        {
            token = preview.GetProperty("token").GetString()
        }));
        var queued = coordinator.History().Single();
        Check(queued.Target == target && queued.Download?.Kind == "local", "local import queue freezes original instance across selection changes");
        var record = await coordinator.Run("import", root, target, operation => new DownloadExecutor(context.Http).ExecuteAsync(queued, operation));
        Check(record.State == OperationState.Completed && File.ReadAllBytes(Path.Combine(target, "mods", "fixture.jar")).SequenceEqual(original), "local import executes Core transactional copy");
        try
        {
            await coordinator.Run("duplicate", root, target, operation => new DownloadExecutor(context.Http).ExecuteAsync(queued, operation));
            throw new Exception("duplicate import accepted");
        }
        catch (IOException) { Check(File.ReadAllBytes(Path.Combine(target, "mods", "fixture.jar")).SequenceEqual(original), "duplicate import preserves existing file"); }
        await commands.Preview(JsonSerializer.SerializeToElement(new
        {
            path = file,
            instance = "1.21.1",
            requestId = "duplicate-preview"
        }));
        Check(!messages.Last().GetProperty("canImport").GetBoolean() && messages.Last().GetProperty("token").GetString() == "", "duplicate is blocked before confirmation without replacing existing file");
        Check(messages.Last().GetProperty("requestId").GetString() == "duplicate-preview", "import preview preserves request identity");
        await commands.Preview(JsonSerializer.SerializeToElement(new
        {
            path = file,
            instance = "other"
        }));
        Check(messages.Last().GetProperty("target").GetString() != target && messages.Last().GetProperty("canImport").GetBoolean(), "explicit import target can differ from current instance");
        var ambiguous = Path.Combine(data, "ambiguous.zip");
        using (var zip = ZipFile.Open(ambiguous, ZipArchiveMode.Create))
        {
            zip.CreateEntry("pack.mcmeta");
            zip.CreateEntry("assets/test/a");
            zip.CreateEntry("data/test/a");
        }
        await commands.Preview(JsonSerializer.SerializeToElement(new
        {
            path = ambiguous,
            instance = "other"
        }));
        Check(messages.Last().GetProperty("kind").GetString() == "ambiguous" && !messages.Last().GetProperty("canImport").GetBoolean(), "ambiguous archive requires explicit use before any queue token");
        await commands.Preview(JsonSerializer.SerializeToElement(new
        {
            path = ambiguous,
            instance = "other",
            resourceKind = "resourcepacks"
        }));
        Check(messages.Last().GetProperty("kind").GetString() == "resourcepacks" && messages.Last().GetProperty("canImport").GetBoolean(), "explicit resource-pack use resolves ambiguous archive");
        var vanilla = Path.Combine(root, "versions", "vanilla");
        Directory.CreateDirectory(vanilla);
        File.WriteAllText(Path.Combine(vanilla, "vanilla.json"), "{\"id\":\"1.21.1\",\"libraries\":[]}");
        try
        {
            await commands.Preview(JsonSerializer.SerializeToElement(new
            {
                path = file,
                instance = "vanilla"
            }));
            throw new Exception("Vanilla accepted Fabric mod");
        }
        catch (IOException) { Check(true, "loader mismatch is rejected before queuing"); }
        var big = Path.Combine(data, "cancel-copy.jar");
        File.WriteAllBytes(big, new byte[200_000]);
        using (var cancellation = new CancellationTokenSource())
        {
            try
            {
                await new ContentService(context.Http, cancellation.Token).ImportFiles(target, "mods", new[] { big }, _ => cancellation.Cancel());
                throw new Exception("Canceled copy completed");
            }
            catch (OperationCanceledException)
            {
                Check(!File.Exists(Path.Combine(target, "mods", "cancel-copy.jar")), "cancel during local copy leaves no partial live file");
            }
        }
        File.WriteAllText(file, "modified");
        try
        {
            await coordinator.Run("changed", root, target, operation => new DownloadExecutor(context.Http).ExecuteAsync(queued, operation));
            throw new Exception("changed source accepted");
        }
        catch (IOException) { Check(true, "source mutation after preview is rejected"); }

    }
}
