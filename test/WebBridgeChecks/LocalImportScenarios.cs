using System.Text.Json;
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
            File.WriteAllText(Path.Combine(directory, key + ".json"), """{"id":"1.21.1","type":"release","libraries":[]}""");
        }
        var store = new ConfigStore(data);
        store.Save(new AppConfig { Root = root, LastInstance = "1.21.1" });
        var messages = new List<JsonElement>();
        using var context = new LauncherContext(value => messages.Add(FixtureMessages.Capture(value)), store);
        var coordinator = new OperationCoordinator(Path.Combine(data, "execution"));
        var commands = new LocalImportCommands(context, (name, gameRoot, directory, request) => coordinator.Enqueue(name, gameRoot, directory, request));
        var file = Path.Combine(data, "fixture.jar");
        File.WriteAllText(file, "local fixture");
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
        Check(record.State == OperationState.Completed && File.ReadAllText(Path.Combine(target, "mods", "fixture.jar")) == "local fixture", "local import executes Core transactional copy");
        try
        {
            await coordinator.Run("duplicate", root, target, operation => new DownloadExecutor(context.Http).ExecuteAsync(queued, operation));
            throw new Exception("duplicate import accepted");
        }
        catch (IOException) { Check(File.ReadAllText(Path.Combine(target, "mods", "fixture.jar")) == "local fixture", "duplicate import preserves existing file"); }
        File.WriteAllText(file, "modified");
        try
        {
            await coordinator.Run("changed", root, target, operation => new DownloadExecutor(context.Http).ExecuteAsync(queued, operation));
            throw new Exception("changed source accepted");
        }
        catch (IOException) { Check(true, "source mutation after preview is rejected"); }

    }
}
