using System.Diagnostics;
using System.IO;
using System.Text.Json;
using BlockLaunch;
using MCUGLauncher.Contracts;
using MCUGLauncher.Models;
using MCUGLauncher.Services;
using MCUGLauncher.UseCases;

internal static class ServerLaunchScenarios
{
    public static async Task Run(string fixture)
    {
        const string favorite = "favorite-fixture";
        const string selected = "selected-fixture";
        var root = Path.Combine(fixture, "server-launch-game");
        foreach (var version in new[] { "1.21.1", favorite, selected })
        {
            var directory = Path.Combine(root, "versions", version);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, version + ".json"), JsonSerializer.Serialize(new
            {
                id = version,
                type = "release",
                inheritsFrom = version == "1.21.1" ? null : "1.21.1"
            }));
        }
        var store = new ConfigStore(Path.Combine(fixture, "server-launch-config"));
        var config = new AppConfig
        {
            Root = root,
            Player = "Fixture",
            LastVersion = selected,
            LastInstance = selected,
            AutoMemory = false,
            Ram = 2048,
            Servers = [new SavedServer { Name = "Favorite", Address = "play.example:25566", Instance = favorite }]
        };
        config.LegacyPreferences.Launch.AutoChinese = false;
        config.LegacyPreferences.Launch.Server = "";
        store.Save(config);
        var catalog = new InstanceCatalog(root);
        var descriptor = catalog.GetOrCreate(favorite);
        descriptor.Launch = new LaunchSettings
        {
            AutoMemory = false,
            AutoChinese = false,
            RamMb = 1536,
            Width = 1000,
            Height = 700,
            JvmArguments = "-Dfixture=server-launch",
            Server = "instance-default.example:25567"
        };
        catalog.Save(descriptor);
        var metadata = Path.Combine(catalog.MetadataDirectory(favorite), ".blocklaunch-instance.json");
        var metadataBefore = File.ReadAllText(metadata);
        using var context = new LauncherContext(_ => { }, store);
        var requests = new List<LaunchRequest>();
        var processes = new List<Process>();
        Task<Process> Build(LaunchRequest request, CancellationToken token)
        {
            requests.Add(request);
            var process = NewProcess();
            processes.Add(process);
            return Task.FromResult(process);
        }
        var commands = new GameCommands(context, Build);

        var noInstance = await commands.LaunchToServerAsync("", "play.example");
        Check(!noInstance.Ok && requests.Count == 0, "unbound favorite never falls back to current instance");
        var noAddress = await commands.LaunchToServerAsync(favorite, " ");
        Check(!noAddress.Ok && requests.Count == 0, "empty favorite address does not start a normal game");
        foreach (var address in new[] { "https://invalid.example", "host:0", "host:65536" })
        {
            var invalid = await commands.LaunchToServerAsync(favorite, address);
            Check(!invalid.Ok && requests.Count == 0, "invalid server endpoint rejected: " + address);
        }
        foreach (var instance in new[] { "missing-instance", "../outside" })
        {
            var invalid = await commands.LaunchToServerAsync(instance, "play.example");
            Check(!invalid.Ok && requests.Count == 0, "invalid instance never uses selected version: " + instance);
        }

        var result = await commands.LaunchToServerAsync(favorite, " play.example:25566 ");
        Check(result.Ok, "favorite launch starts the constructed process");
        await Finish(context, processes);
        var request = requests.Last();
        Check(request.Profile == favorite && request.Root == root && request.InstanceDirectory == catalog.DirectoryFor(favorite),
            "launch uses the explicit favorite instance and its data directory");
        Check(request.Settings!.Server == "play.example:25566", "server address reaches the actual launch request");
        Check(request.Ram == 1536 && request.Width == 1000 && request.Settings.JvmArguments == "-Dfixture=server-launch",
            "server launch preserves the instance launch settings");
        Check(request.Session.Username == "Fixture", "server launch uses the existing account flow");
        Check(File.ReadAllText(metadata) == metadataBefore && store.Load().LegacyPreferences.Launch.Server == "",
            "temporary server override does not modify instance or global settings");
        Check(store.Load().Servers.Single().Address == "play.example:25566",
            "starting a favorite does not rewrite saved favorites");

        result = await commands.LaunchToServerAsync(favorite, "play.example");
        Check(result.Ok, "server without port starts");
        await Finish(context, processes);
        Check(requests.Last().Settings!.Server == "play.example:25565", "missing port uses the default server port");
        result = await commands.LaunchToServerAsync(favorite, "[::1]:25566");
        Check(result.Ok, "IPv6 server request starts");
        await Finish(context, processes);
        Check(requests.Last().Settings!.Server == "[::1]:25566", "IPv6 brackets and port are retained");

        result = await commands.LaunchAsync(selected);
        Check(result.Ok, "ordinary game launch still starts");
        await Finish(context, processes);
        Check(requests.Last().Settings!.Server == "", "ordinary unconfigured launch does not retain favorite address");
        result = await commands.LaunchAsync(favorite);
        Check(result.Ok, "ordinary instance launch still starts");
        await Finish(context, processes);
        Check(requests.Last().Settings!.Server == "instance-default.example:25567",
            "ordinary launch preserves the instance configured server");

        var failing = new GameCommands(context, (_, _) => Task.FromException<Process>(new IOException("fixture process failure")));
        var failed = await failing.LaunchToServerAsync(favorite, "play.example");
        Check(!failed.Ok && failed.Message.Contains("fixture process failure", StringComparison.Ordinal)
            && !context.IsInstanceInUse(catalog.DirectoryFor(favorite)), "failed construction returns failure and releases reservation");
        result = await commands.LaunchToServerAsync(favorite, "play.example");
        Check(result.Ok, "server launch can retry after a construction failure");
        await Finish(context, processes);

        var gate = new TaskCompletionSource<Process>(TaskCreationOptions.RunContinuationsAsynchronously);
        var waiting = new GameCommands(context, (_, _) => gate.Task);
        var first = waiting.LaunchToServerAsync(favorite, "play.example");
        Check(!first.IsCompleted && context.IsInstanceInUse(catalog.DirectoryFor(favorite)),
            "pending server launch reserves the instance");
        var previousCount = requests.Count;
        var second = await new GameCommands(context, Build).LaunchAsync(favorite);
        var processForGate = NewProcess();
        processes.Add(processForGate);
        gate.SetResult(processForGate);
        Check((await first).Ok, "reserved launch completes after preparation");
        await Finish(context, processes);
        Check(!second.Ok && requests.Count == previousCount, "ordinary and favorite entry points cannot prepare the same instance concurrently");

        using ILauncherService service = new LauncherService(new NoDesktop(), action => action(), _ => null!, _ => "", store);
        var viaInterface = await service.LaunchToServerAsync("", "play.example");
        Check(!viaInterface.Ok && viaInterface.Message.Contains("绑定", StringComparison.Ordinal),
            "public typed service routes to favorite launch validation");
        viaInterface = await service.LaunchToServerAsync("missing-instance", "play.example");
        Check(!viaInterface.Ok && viaInterface.Message.Contains("不存在", StringComparison.Ordinal),
            "public typed service reports missing bound instance");
        viaInterface = await service.LaunchToServerAsync(favorite, "https://invalid.example");
        Check(!viaInterface.Ok, "public typed service rejects invalid server before attempting game startup");
        Check(File.ReadAllText(metadata) == metadataBefore, "all server launches leave saved instance settings unchanged");
        foreach (var process in processes)
            process.Dispose();
    }

    static Process NewProcess()
    {
        var info = new ProcessStartInfo(OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        info.ArgumentList.Add(OperatingSystem.IsWindows() ? "/c" : "-c");
        info.ArgumentList.Add("exit 0");
        return new Process { StartInfo = info };
    }

    static async Task Finish(LauncherContext context, IEnumerable<Process> processes)
    {
        foreach (var process in processes)
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        foreach (var game in context.Games.Active)
            await game.Session.Completion.WaitAsync(TimeSpan.FromSeconds(10));
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
