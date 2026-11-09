using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using BlockLaunch;
using MCUGLauncher.Contracts;
using MCUGLauncher.Models;
using MCUGLauncher.Services;
using MCUGLauncher.UseCases;

internal static class GameDirectoryScenarios
{
    public static async Task Run(string fixture)
    {
        const string key = "1.21.1";
        var first = Path.Combine(fixture, "directory-a");
        var second = Path.Combine(fixture, "directory-b");
        var missing = Path.Combine(fixture, "offline-drive");
        foreach (var root in new[] { first, second })
        {
            var directory = Path.Combine(root, "versions", key);
            Directory.CreateDirectory(Path.Combine(directory, "mods"));
            File.WriteAllText(Path.Combine(directory, key + ".json"), "{\"id\":\"1.21.1\",\"type\":\"release\"}");
            File.WriteAllText(Path.Combine(directory, "mods", "fixture.jar"), root);
            File.WriteAllText(Path.Combine(directory, "options.txt"), root);
        }
        var store = new ConfigStore(Path.Combine(fixture, "multi-directory-config"));
        var initial = new AppConfig
        {
            Root = first,
            LastVersion = key,
            LastInstance = key,
            Player = "Fixture",
            AutoMemory = false,
            GameRoots = [first, missing],
            Servers = [new SavedServer { Name = "Legacy", Address = "legacy.example", Instance = key }]
        };
        initial.LegacyPreferences.Launch.AutoChinese = false;
        store.Save(initial);
        using ILauncherService service = new LauncherService(new NoDesktop(), action => action(), _ => null!, _ => "", store);
        await service.AddGameDirectoryAsync(second + Path.DirectorySeparatorChar);
        await service.AddGameDirectoryAsync(Path.Combine(second, "."));
        Check(store.Load().GameRoots.Count == 3 && store.Load().Root == first, "add normalizes deduplicates persists without selecting");
        var scan = await service.ScanGameDirectoriesAsync();
        Check(scan.Count == 3 && scan.Where(item => item.Available).Sum(item => item.Instances.Count) == 2,
            "all directories scanned with duplicate instance names preserved");
        Check(scan.Single(item => item.Root == missing).Error.Length > 0 && !scan.Single(item => item.Root == missing).Available,
            "offline directory reports failure without hiding other directories");
        Check(scan.Where(item => item.Available).All(item => item.Instances.Single().Root == item.Root), "each instance carries its root");
        var corrupt = Path.Combine(fixture, "corrupt-directory");
        Directory.CreateDirectory(Path.Combine(corrupt, "versions", key));
        File.WriteAllText(Path.Combine(corrupt, "versions", key, key + ".json"), "{\"id\":\"1.21.1\"}");
        Directory.CreateDirectory(Path.Combine(corrupt, "instances", key));
        File.WriteAllText(Path.Combine(corrupt, "instances", key, ".blocklaunch-instance.json"), "{broken");
        await service.AddGameDirectoryAsync(corrupt);
        var damaged = await service.ScanGameDirectoriesAsync();
        Check(!damaged.Single(item => item.Root == corrupt).Available && damaged.Count(item => item.Available) == 2,
            "corrupt metadata isolated to its directory");
        await service.RemoveGameDirectoryAsync(corrupt);
        await service.RequestStateAsync();
        Check(service.State!.GameDirectories.Count == 3 && service.State.Instances.Count == 1 && service.State.Instances.Single().Root == first,
            "state includes all scans and preserves legacy current-root list");
        Check(store.Load().Servers.Single().GameRoot == first, "legacy favorite root pinned before selection changes");
        await service.SelectManagedInstanceAsync(new(second, key));
        Check(store.Load().Root == second && store.Load().LastInstance == key && service.State!.Root == second,
            "selection persists both root and instance name");
        await service.SaveServerAsync("Bound", "bound.example", instance: key, gameRoot: first);
        Check(store.Load().Servers.Single(item => item.Address == "bound.example").GameRoot == first, "favorite saves explicit instance root");
        var mods = await service.ListManagedModsAsync(new(first, key));
        Check(mods.Single().Path.StartsWith(first, StringComparison.Ordinal), "explicit mod query ignores selected root");
        await service.ToggleManagedModAsync(new(first, key), "fixture.jar", false);
        Check(File.Exists(Path.Combine(first, "versions", key, "mods", "fixture.jar.disabled")) &&
            File.Exists(Path.Combine(second, "versions", key, "mods", "fixture.jar")), "same-name mod changes only requested root");
        var preview = await service.PreviewManagedUninstallAsync(new(first, key));
        Check(preview.Paths.Any(path => path.StartsWith(first, StringComparison.Ordinal)) &&
            !preview.Paths.Any(path => path.StartsWith(second, StringComparison.Ordinal)), "uninstall preview stays in requested directory");
        await service.ApplyUninstallAsync(preview.Token);
        Check(Directory.Exists(Path.Combine(first, "versions", key)), "uninstall requires desktop confirmation");
        await Reject(() => service.SelectManagedInstanceAsync(new(Path.Combine(fixture, "unknown"), key)), "unregistered root rejected");
        await Reject(() => service.SelectManagedInstanceAsync(new("", key)), "empty target cannot fall back to selected root");
        await Reject(() => service.AddGameDirectoryAsync(missing), "new missing directory rejected");
        await Reject(() => service.AddGameDirectoryAsync("relative-directory"), "relative directory rejected");
        Check(store.Load().Root == second && store.Load().GameRoots.Count == 3, "invalid requests do not mutate saved selection");

        using (var context = new LauncherContext(_ => { }, store))
        {
            var captured = new TaskCompletionSource<LaunchRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
            var proceed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var commands = new GameCommands(context, async (request, _) =>
            {
                captured.SetResult(request);
                await proceed.Task;
                var info = new ProcessStartInfo(OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                foreach (var argument in OperatingSystem.IsWindows() ? new[] { "/d", "/c", "exit 0" } : new[] { "-c", "exit 0" })
                    info.ArgumentList.Add(argument);
                return new Process { StartInfo = info };
            });
            var launching = commands.LaunchManagedAsync(new(first, key), "play.example:25566");
            var request = await captured.Task.WaitAsync(TimeSpan.FromSeconds(10));
            context.Config.Root = second;
            proceed.SetResult();
            Check((await launching).Ok && request.Root == first && request.Settings!.Server == "play.example:25566",
                "launch freezes explicit root and server before await");
            await Task.WhenAll(context.Games.Active.Select(game => game.Session.Completion));
            LaunchRequest? favoriteRequest = null;
            var favoriteCommands = new GameCommands(context, (value, _) =>
            {
                favoriteRequest = value;
                return Task.FromResult(CreateProcess());
            });
            var favorite = await favoriteCommands.LaunchToServerAsync(key, "bound.example:25565");
            Check(favorite.Ok && favoriteRequest?.Root == first, "legacy favorite launch uses saved root and normalized server address");
            await Task.WhenAll(context.Games.Active.Select(game => game.Session.Completion));
        }

        // The real queue persists the explicit snapshot before any directory changes.
        using (var context = new LauncherContext(_ => { }, store))
        {
            var destination = Path.Combine(fixture, "multi-directory-export.zip");
            var command = new InstanceMaintenanceCommands(context);
            command.Export(JsonSerializer.SerializeToElement(new
            {
                path = destination
            }), new(first, key));
            context.Config.Root = second;
            context.Config.GameRoots.Remove(first);
            await context.Tasks.Completion.WaitAsync(TimeSpan.FromSeconds(20));
            var operation = context.Tasks.History().Single();
            Check(operation.Root == first && operation.Download!.Instance!.Key == key && operation.State == OperationState.Completed,
                "queued operation keeps root after selection and directory records change");
            using var zip = ZipFile.OpenRead(destination);
            using var reader = new StreamReader(zip.Entries.Single(entry => entry.FullName.EndsWith("fixture.jar.disabled", StringComparison.Ordinal)).Open());
            Check(await reader.ReadToEndAsync() == first, "export contains requested instance bytes");
        }
        using (var context = new LauncherContext(_ => { }, store))
        {
            var dispatcher = new FeatureDispatcher(context);
            await dispatcher.ExecuteAsync<SavedInfo>(nameof(ILauncherService.CloneManagedInstanceAsync), new
            {
                target = new InstanceTarget(first, key),
                name = "Directory copy",
                saves = false,
                servers = false
            });
            context.Config.Root = second;
            await context.Tasks.Completion.WaitAsync(TimeSpan.FromSeconds(20));
            var operation = context.Tasks.History().Single(item => item.Download?.Kind == "clone");
            Check(operation.State == OperationState.Completed && operation.Root == first &&
                Directory.EnumerateDirectories(Path.Combine(first, "instances")).Any(path =>
                    File.Exists(Path.Combine(path, "mods", "fixture.jar.disabled"))), "clone feature uses explicit directory and real task queue");
        }
        await service.DeleteManagedModAsync(new(first, key), "fixture.jar");
        Check(!File.Exists(Path.Combine(first, "versions", key, "mods", "fixture.jar.disabled")) &&
            File.Exists(Path.Combine(second, "versions", key, "mods", "fixture.jar")), "mod deletion cannot touch same-name instance in other root");
        await service.RemoveGameDirectoryAsync(first);
        Check(Directory.Exists(Path.Combine(first, "versions", key)) && !store.Load().GameRoots.Contains(first), "remove unregisters directory without deleting game files");
        await service.RemoveGameDirectoryAsync(second);
        Check(store.Load().Root == missing, "offline remaining root remains selected after reload");
        await Reject(() => service.RemoveGameDirectoryAsync(missing), "last directory cannot be removed");
        Check((await service.ScanGameDirectoriesAsync()).Single().Root == missing, "offline record survives restart and refresh");
    }

    static Process CreateProcess()
    {
        var info = new ProcessStartInfo(OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in OperatingSystem.IsWindows() ? new[] { "/d", "/c", "exit 0" } : new[] { "-c", "exit 0" })
            info.ArgumentList.Add(argument);
        return new Process { StartInfo = info };
    }

    static async Task Reject(Func<Task> action, string name)
    {
        try
        {
            await action();
        }
        catch (ArgumentException) { Check(true, name); return; }
        catch (IOException) { Check(true, name); return; }
        throw new Exception("Expected rejection: " + name);
    }

    static void Check(bool condition, string name)
    {
        if (!condition)
            throw new Exception(name);
        Console.WriteLine("PASS " + name);
    }

    sealed class NoDesktop : IDesktopHost
    {
        public bool ConfirmStopGame() => false;
        public bool ConfirmUninstall(string name) => false;
        public string? PickSaveFile(string target) => null;
        public string? PickFolder(string target) => null;
        public string? PickFile(string target) => null;
        public void Window(string action)
        {
        }
        public void Drag()
        {
        }
        public void ResetWindow()
        {
        }
    }
}
