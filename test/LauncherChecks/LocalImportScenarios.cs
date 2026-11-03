using System.IO;
using System.IO.Compression;
using System.Text.Json;
using BlockLaunch;
using MCUGLauncher.Models;
using MCUGLauncher.Services;
using MCUGLauncher.UseCases;

internal static class LocalImportScenarios
{
    public static async Task Run(string fixture)
    {
        var root = Path.Combine(fixture, "local-import-game");
        var vanillaDirectory = Path.Combine(root, "versions", "1.21.1");
        var versionDirectory = Path.Combine(root, "versions", "fabric-import");
        Directory.CreateDirectory(vanillaDirectory);
        Directory.CreateDirectory(versionDirectory);
        File.WriteAllText(Path.Combine(vanillaDirectory, "1.21.1.json"), """{"id":"1.21.1","type":"release"}""");
        File.WriteAllText(Path.Combine(versionDirectory, "fabric-import.json"),
            """{"id":"fabric-import","inheritsFrom":"1.21.1","libraries":[{"name":"net.fabricmc:fabric-loader:0.16.0"}]}""");

        var file = Path.Combine(fixture, "local-resource.zip");
        using (var zip = ZipFile.Open(file, ZipArchiveMode.Create))
        {
            using var writer = new StreamWriter(zip.CreateEntry("pack.mcmeta").Open());
            writer.Write("{}");
        }
        using (var zip = ZipFile.Open(file, ZipArchiveMode.Update))
        {
            using var writer = new StreamWriter(zip.CreateEntry("assets/test/file.txt").Open());
            writer.Write("resource data");
        }

        var store = new ConfigStore(Path.Combine(fixture, "local-import-config"));
        store.Save(new AppConfig { Root = root, LastInstance = "fabric-import", LastVersion = "fabric-import" });
        using var context = new LauncherContext(_ => { }, store);
        var preview = await context.LocalImports.PreviewAsync(file, "fabric-import", "");
        Check(preview.CanImport && preview.Kind == "resourcepacks" && preview.Target.Length > 0,
            "local import preview classifies file and freezes selected instance");
        await context.LocalImports.ConfirmAsync(preview.Token);
        await context.Tasks.Completion;
        var target = InstanceSnapshot.Capture(context.Config, "fabric-import").Directory;
        Check(File.ReadAllBytes(Path.Combine(target, "resourcepacks", Path.GetFileName(file))).SequenceEqual(File.ReadAllBytes(file))
            && context.Tasks.History().Single().State == OperationState.Completed,
            "confirmed local import uses the persistent task queue and Core transaction");

        var conflict = await context.LocalImports.PreviewAsync(file, "fabric-import", "");
        Check(!conflict.CanImport && conflict.Token.Length == 0
            && conflict.Warnings.Any(warning => warning.Contains("不会覆盖", StringComparison.Ordinal)),
            "local import conflict is previewed without overwriting existing files");

        var dataPack = Path.Combine(fixture, "local-datapack.zip");
        using (var zip = ZipFile.Open(dataPack, ZipArchiveMode.Create))
        {
            using var writer = new StreamWriter(zip.CreateEntry("pack.mcmeta").Open());
            writer.Write("{}");
        }
        using (var zip = ZipFile.Open(dataPack, ZipArchiveMode.Update))
        {
            using var writer = new StreamWriter(zip.CreateEntry("data/example/file.json").Open());
            writer.Write("{}");
        }
        var unsupported = await context.LocalImports.PreviewAsync(dataPack, "fabric-import", "");
        Check(!unsupported.CanImport && unsupported.Kind == "datapacks" && unsupported.Warnings.Count > 0,
            "datapack preview explains that a save target is required");

        var changed = Path.Combine(fixture, "changed-resource.zip");
        File.Copy(file, changed);
        var queued = (Name: "", Root: "", Target: "", Request: (DownloadRequest?)null);
        var capturing = new LocalImportCommands(context, (name, taskRoot, taskTarget, request) =>
        {
            queued = (name, taskRoot, taskTarget, request);
        });
        var queuedPreview = await capturing.PreviewAsync(changed, "fabric-import", "");
        context.Config.Root = Path.Combine(fixture, "different-root");
        await capturing.ConfirmAsync(queuedPreview.Token);
        Check(queued.Root == root && queued.Target == target && queued.Request?.Kind == "local",
            "confirmed import remains bound to its previewed root and instance");

        context.Config.Root = root;
        var nullContext = await CaptureTypedPreview(context, file);
        Check(nullContext.Kind == "resourcepacks", "typed local import feature returns its declared DTO");
    }

    static Task<MCUGLauncher.Contracts.LocalImportPreviewInfo> CaptureTypedPreview(LauncherContext context, string file)
    {
        var handlers = new FeatureDispatcher(context);
        return handlers.ExecuteAsync<MCUGLauncher.Contracts.LocalImportPreviewInfo>(
            nameof(MCUGLauncher.Contracts.ILauncherService.PreviewLocalImportAsync),
            new
            {
                path = file,
                instance = "fabric-import",
                resourceKind = ""
            });
    }

    static void Check(bool condition, string name)
    {
        if (!condition)
            throw new Exception(name);
        Console.WriteLine("PASS " + name);
    }
}
