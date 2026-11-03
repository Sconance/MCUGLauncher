using MCUGLauncher.Contracts;
using MCUGLauncher.Models;
using MCUGLauncher.Services;
using MCUGLauncher.UseCases;

var root = Path.Combine(Path.GetTempPath(), "mcug-contract-" + Guid.NewGuid().ToString("N"));
var game = Path.Combine(root, "game");
var version = Path.Combine(game, "versions", "1.21.1");
Directory.CreateDirectory(version);
File.WriteAllText(Path.Combine(version, "1.21.1.json"), """{"id":"1.21.1","type":"release","libraries":[]}""");
var store = new ConfigStore(root);
store.Save(new AppConfig { Root = game, LastVersion = "1.21.1", LastInstance = "1.21.1", Player = "Before" });
using ILauncherService service = new LauncherService(new Desktop(), action => action(), _ => throw new NotSupportedException("No real login in isolated checks"), _ => "", store);
void Check(bool value, string text) { if (!value) throw new Exception(text); Console.WriteLine("PASS " + text); }
var states = 0;
service.StateChanged += _ => states++;
await service.RequestStateAsync();
Check(service.State.Instances.Single().Name == "1.21.1" && states > 0, "typed service exposes actual catalog state");
var saved = await service.SaveSettingsAsync(new AppSettingsPatch
{
    Player = "After",
    BackupCount = 9,
    Network = new NetworkSettingsInfo { DownloadSource = "仅官方", ProxyMode = "直连", LimitKibPerSecond = 128 },
    Appearance = new AppearancePatch { Path = "", Opacity = 0.4, Fit = "完整显示" }
});
Check(saved.Ok && store.Load().Player == "After" && service.State.Preferences.SaveBackupCount == 9, "typed settings patch and success receipt preserve added capabilities");
Check(service.State.Preferences.Network.LimitKibPerSecond == 128 && service.State.Preferences.BackgroundOpacity == 0.4, "network and appearance snapshots match persisted values");
var before = service.State;
await service.SaveSettingsAsync(new AppSettingsPatch { BackupCount = 7 });
Check(before.Preferences.SaveBackupCount == 9, "published snapshot does not track later mutable configuration edits");
var options = await service.ReadLaunchOptionsAsync();
Check(options.Settings.AutoChinese, "automatic Chinese survives the typed contract");
await service.SaveLaunchOptionsAsync(options.Token, "", false, options.Settings with { AutoChinese = false });
Check(!store.Load().LegacyPreferences.Launch.AutoChinese, "typed launch settings persist Chinese opt-out");
try
{
    await service.SaveSettingsAsync(new AppSettingsPatch { Player = "Invalid", BackupCount = 99 });
    throw new Exception("invalid edit accepted");
}
catch (IOException) { Check(store.Load().Player == "After", "typed operation errors do not report success or mutate unrelated values"); }
var server = await service.SaveServerAsync("Fixture", "localhost:25565");
Check(server.Ok && service.State.Servers.Single().Address == "localhost:25565", "typed server operation updates state");
var feedback = new List<string>();
service.FeedbackReceived += feedback.Add;
await service.LaunchServerAsync(new ServerLaunchInfo("1.21.1", "host:70000"));
Check(feedback.Count == 1, "typed server launch routes validation failures to existing feedback");
await service.LaunchServerAsync(new ServerLaunchInfo("", "localhost:25565"));
Check(feedback.Count == 2 && service.Games.Length == 0, "server launch requires an explicit instance instead of falling back to selection");
Check(typeof(ILauncherService).GetMethods().All(m => !m.GetParameters().Any(p => p.ParameterType == typeof(object))), "UI service has no object command escape hatch");
Console.WriteLine("ALL_LAUNCHER_CHECKS_PASSED (real local use cases, no account/network/game)");

sealed class Desktop : IDesktopHost
{
    public Task<bool> ConfirmStopGameAsync() => Task.FromResult(false);
    public Task<bool> ConfirmUninstallAsync(string name) => Task.FromResult(false);
    public Task<string?> PickFolderAsync(string target) => Task.FromResult<string?>(null);
    public Task<string?> PickFileAsync(string target) => Task.FromResult<string?>(null);
    public Task<string?> PickSaveFileAsync(string target) => Task.FromResult<string?>(null);
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
