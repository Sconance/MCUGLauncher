using System.Diagnostics;
using System.Text.Json;
using MCUGLauncher;
using MCUGLauncher.Models;
using MCUGLauncher.Services;
using MCUGLauncher.UseCases;

internal static class SettingsScenarios
{
    static JsonElement Message(object value) => JsonSerializer.SerializeToElement(value);
    static void Check(bool value, string label)
    {
        if (!value)
            throw new Exception(label);
        Console.WriteLine("PASS " + label);
    }
    public static async Task Run(string fixture)
    {
        var root = Path.Combine(fixture, "settings-game");
        Directory.CreateDirectory(root);
        var store = new ConfigStore(Path.Combine(fixture, "settings-config"));
        store.Save(new AppConfig { Root = root, Player = "SettingsFixture", Ram = 1536 });
        using var context = new LauncherContext(_ => { }, store);
        var commands = new SettingsCommands(context);
        var network = context.Config.LegacyPreferences.Network;
        commands.SaveSettings(Message(new
        {
            network = new
            {
                downloadSource = "BMCLAPI 优先",
                proxyMode = "直连",
                proxyAddress = "",
                limitKibPerSecond = 512
            },
            backupCount = 7,
            resourceDownloadDirectory = Path.Combine(fixture, "downloads")
        }));
        var saved = store.Load();
        Check(saved.LegacyPreferences.Network.LimitKibPerSecond == 512 && saved.LegacyPreferences.SaveBackupCount == 7 && saved.Ram == 1536, "network and backup settings persist without resetting launch memory");
        Check(ReferenceEquals(network, context.Config.LegacyPreferences.Network), "saving network settings preserves active HTTP policy reference");
        try
        {
            commands.SaveSettings(Message(new
            {
                player = "ShouldNotChange",
                network = new
                {
                    downloadSource = "仅官方",
                    proxyMode = "直连",
                    proxyAddress = "",
                    limitKibPerSecond = -1
                }
            }));
            throw new Exception("invalid speed accepted");
        }
        catch (IOException) { Check(context.Config.Player == "SettingsFixture" && network.LimitKibPerSecond == 512, "invalid network settings reject the complete edit before mutation"); }
        try
        {
            commands.SaveSettings(Message(new
            {
                appearance = new
                {
                    path = "",
                    opacity = 2.0,
                    fit = "拉伸"
                }
            }));
            throw new Exception("invalid opacity accepted");
        }
        catch (ArgumentException) { Check(true, "invalid background opacity rejected"); }
        var other = Path.Combine(fixture, "directory-record");
        Directory.CreateDirectory(other);
        File.WriteAllText(Path.Combine(other, "keep.txt"), "keep");
        commands.SaveSettings(Message(new
        {
            gameRoots = new[] { root, other }
        }));
        commands.SaveSettings(Message(new
        {
            gameRoots = Array.Empty<string>()
        }));
        Check(context.Config.GameRoots.SequenceEqual(new[] { root }) && File.Exists(Path.Combine(other, "keep.txt")), "removing directory records keeps active root and preserves files");
        await LanguageCase(fixture, "1.21.1", true, "zh_cn");
        await LanguageCase(fixture, "1.7.10", true, "zh_CN");
        await LanguageCase(fixture, "1.21.1", false, "en_us");
    }
    static async Task LanguageCase(string fixture, string minecraft, bool enabled, string expected)
    {
        var data = Path.Combine(fixture, "language-" + minecraft + "-" + enabled);
        var root = Path.Combine(data, "game");
        var directory = Path.Combine(root, "versions", minecraft);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, minecraft + ".json"), JsonSerializer.Serialize(new
        {
            id = minecraft,
            type = "release",
            libraries = Array.Empty<object>()
        }));
        var store = new ConfigStore(data);
        var config = new AppConfig { Root = root, Player = "LanguageFixture", LastVersion = minecraft, LastInstance = minecraft };
        config.LegacyPreferences.Launch.AutoChinese = enabled;
        store.Save(config);
        using var context = new LauncherContext(_ => { }, store);
        var instance = InstanceSnapshot.Capture(context.Config, minecraft);
        Directory.CreateDirectory(instance.Directory);
        File.WriteAllLines(Path.Combine(instance.Directory, "options.txt"), ["lang:en_us", "renderDistance:12"]);
        var checkedLanguage = false;
        var launch = new GameCommands(context, (request, _) =>
        {
            var lines = File.ReadAllLines(Path.Combine(request.InstanceDirectory, "options.txt"));
            Check(lines.Contains("lang:" + expected) && lines.Contains("renderDistance:12"), "launch language policy " + minecraft + " enabled=" + enabled + " preserves other options");
            checkedLanguage = true;
            var process = new Process { StartInfo = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, ArgumentList = { "/c", "exit", "0" } } };
            return Task.FromResult(process);
        });
        await launch.LaunchAsync(minecraft);
        await Task.WhenAll(context.Games.Active.Select(game => game.Session.Completion));
        Check(checkedLanguage, "language policy reaches the captured launch target");
    }
}
