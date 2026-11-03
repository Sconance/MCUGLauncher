using System.Text.Json;
using MCUGLauncher;
using MCUGLauncher.Contracts;
using MCUGLauncher.Models;
using MCUGLauncher.Services;
using MCUGLauncher.UseCases;

internal static class ServerLaunchScenarios
{
    public static async Task Run(string fixture)
    {
        var root = Path.Combine(fixture, "server-launch");
        var game = Path.Combine(root, "game");
        var profile = Path.Combine(game, "versions", "1.21.1", "1.21.1.json");
        Directory.CreateDirectory(Path.GetDirectoryName(profile)!);
        File.WriteAllText(profile, """{"id":"1.21.1","type":"release","libraries":[]}""");
        var store = new ConfigStore(root);
        store.Save(new AppConfig
        {
            Root = game,
            Player = "Fixture",
            AccountType = "offline",
            LastVersion = "different-selection",
            LastInstance = "different-selection"
        });
        var catalog = new InstanceCatalog(game);
        var descriptor = catalog.GetOrCreate("1.21.1");
        descriptor.Launch = new LaunchSettings
        {
            AutoMemory = false,
            AutoChinese = false,
            RamMb = 2048,
            Server = "saved.example.com"
        };
        catalog.Save(descriptor);
        var metadata = Path.Combine(catalog.MetadataDirectory(descriptor.Key), ".mcuglauncher-instance.json");
        var original = File.ReadAllText(metadata);
        var messages = new List<JsonElement>();
        using var context = new LauncherContext(value => messages.Add(FixtureMessages.Capture(value)), store);
        LaunchRequest? captured = null;
        var builds = 0;
        var commands = new GameCommands(context, (request, token) =>
        {
            captured = request;
            builds++;
            throw new IOException("fixture: stop before starting a process");
        });
        var input = new ServerLaunchInfo("1.21.1", "favorite.example.com");
        await commands.LaunchServerAsync(input);
        Check(builds == 1 && captured!.Profile == input.InstanceId && captured.Root == game,
            "server launch uses the supplied instance instead of current selection");
        Check(captured!.Settings!.Server == "favorite.example.com:25565" && captured.Ram == 2048 && captured.Session.Username == "Fixture",
            "server launch forwards normalized address, instance settings and authenticated session");
        Check(File.ReadAllText(metadata) == original && context.Config.LastVersion == "different-selection",
            "server launch does not persist its address or save selection on build failure");
        Check(!context.IsRootPreparing(game) && context.Games.Active.Count == 0 && !messages.Last().GetProperty("ok").GetBoolean(),
            "server launch failure releases preparation and reports failure without registering a game");
        await commands.LaunchServerAsync(input with
        {
            ServerAddress = "[::1]:25566"
        });
        Check(builds == 2 && captured!.Settings!.Server == "[::1]:25566", "server launch accepts IPv6 and can retry after failure");
        foreach (var invalid in new[]
        {
            input with { InstanceId = "" },
            input with { InstanceId = "missing" },
            input with { ServerAddress = "" },
            input with { ServerAddress = null! },
            input with { ServerAddress = "host:70000" }
        })
        {
            await commands.LaunchServerAsync(invalid);
            Check(builds == 2 && !messages.Last().GetProperty("ok").GetBoolean(), "invalid server launch rejected: " + invalid);
        }
        descriptor.Launch.Isolation = "共享根目录";
        catalog.Save(descriptor);
        await commands.LaunchServerAsync(input);
        Check(builds == 3 && captured!.InstanceDirectory == game && !context.IsRootPreparing(game),
            "shared-root server launch uses the same directory for guards and process creation");
        context.Config.AccountType = "microsoft";
        context.AccountSessions.RememberIdentity("microsoft", new("Fixture", Guid.NewGuid().ToString("N")));
        await commands.LaunchServerAsync(input);
        Check(builds == 3 && !messages.Last().GetProperty("ok").GetBoolean() && !context.IsRootPreparing(game),
            "server launch cannot bypass online authorization with a locally selected identity");
        context.Config.AccountType = "offline";
        await commands.LaunchAsync("1.21.1");
        Check(builds == 4 && captured!.Settings!.Server == "saved.example.com", "ordinary launch retains its saved server setting");
    }

    static void Check(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
        Console.WriteLine("PASS " + message);
    }
}
