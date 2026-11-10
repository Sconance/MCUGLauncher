using System.IO;
using System.Text.Json;
using MCUGLauncher.Contracts;
using MCUGLauncher.Models;
using MCUGLauncher.Services;
using MCUGLauncher.UseCases;

internal static class ServerFavoriteScenarios
{
    public static async Task Run(string fixture)
    {
        var directory = Path.Combine(fixture, "server-favorite-bindings");
        var game = Path.Combine(directory, "game");
        Directory.CreateDirectory(game);
        var store = new ConfigStore(directory);
        File.WriteAllText(Path.Combine(directory, "config.json"), JsonSerializer.Serialize(new
        {
            Root = game,
            Player = "Fixture",
            Servers = new[] { new { Name = "Legacy", Address = "legacy.example" } }
        }));
        Check(store.Load().Servers.Single().Instance == "", "old config defaults to an unbound favorite");
        using ILauncherService service = new LauncherService(new NoDesktop(), action => action(),
            _ => null!, _ => "", store);
        await service.RequestStateAsync();
        Check(service.State!.Servers.Single().Instance == "", "old binding is available through typed state");

        var added = await service.SaveServerAsync("Bound", "bound.example:25566", instance: " fabric-fixture ");
        Check(added.Ok && store.Load().Servers.Single(s => s.Address == "bound.example:25566").Instance == "fabric-fixture",
            "typed save persists a trimmed instance binding");
        var snapshot = service.State!.Servers.Single(s => s.Address == "bound.example:25566");
        Check(snapshot.Instance == "fabric-fixture", "state snapshot includes saved instance binding");

        await service.SaveServerAsync("Renamed", "renamed.example:25566", "BOUND.EXAMPLE:25566");
        Check(store.Load().Servers.Single(s => s.Address == "renamed.example:25566").Instance == "fabric-fixture",
            "old three-argument address edit retains binding");
        await service.SaveServerAsync("Renamed again", "RENAMED.EXAMPLE:25566");
        Check(store.Load().Servers.Count == 2 && store.Load().Servers.Single(s => s.Name == "Renamed again").Instance == "fabric-fixture",
            "same-address edit deduplicates case-insensitively without losing binding");

        var before = File.ReadAllText(Path.Combine(directory, "config.json"));
        var invalid = await service.SaveServerAsync("Invalid", "", instance: "other-instance");
        Check(!invalid.Ok && File.ReadAllText(Path.Combine(directory, "config.json")) == before,
            "invalid address does not mutate persisted bindings");

        await service.SaveServerAsync("Cleared", "renamed.example:25566", instance: "");
        Check(store.Load().Servers.Single(s => s.Address == "renamed.example:25566").Instance == "",
            "explicit empty instance clears binding");
        Check(snapshot.Instance == "fabric-fixture", "previous state snapshot is independent of later edits");
        Check(!JsonSerializer.Serialize(store.Load().Servers).Contains("\"Id\"", StringComparison.Ordinal),
            "favorites do not acquire server IDs");

        await service.SaveServerAsync("Reload", "reload.example", instance: "another-instance");
        using ILauncherService reloaded = new LauncherService(new NoDesktop(), action => action(),
            _ => null!, _ => "", new ConfigStore(directory));
        await reloaded.RequestStateAsync();
        Check(reloaded.State!.Servers.Single(s => s.Address == "reload.example").Instance == "another-instance",
            "a new service reads the persisted instance binding");

        await service.DeleteServerAsync("renamed.example:25566");
        Check(store.Load().Servers.Count == 2 && store.Load().Servers.Any(s => s.Address == "legacy.example"),
            "deleting a bound favorite preserves other favorites");
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
