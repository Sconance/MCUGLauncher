using System.IO;
using System.Net.Http;
using System.Net;
using System.Text.Json;
using System.Text;
using MCUGLauncher.Services;
using MCUGLauncher.UseCases;
using MCUGLauncher;

var fixture = Path.Combine(Path.GetTempPath(), "mcug-web-check-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(fixture);
var store = new ConfigStore(fixture);
var gameRoot = Path.Combine(fixture, "game");
var versionDir = Path.Combine(gameRoot, "versions", "custom-fabric");
Directory.CreateDirectory(versionDir);
File.WriteAllText(Path.Combine(versionDir, "custom-fabric.json"), """{"id":"custom-fabric","inheritsFrom":"1.21.1"}""");
store.Save(new MCUGLauncher.Models.AppConfig { Root = gameRoot, Player = "Fixture", LastVersion = "custom-fabric", LastInstance = "custom-fabric" });
var messages = new List<JsonElement>();
var desktop = new FakeDesktop();
using var application = new LauncherApplication(value => messages.Add(FixtureMessages.Capture(value)), desktop, store);
bool Has(string type) => messages.Any(m => m.GetProperty("type").GetString() == type);
void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); }
async Task Request(string json) { messages.Clear(); await application.HandleAsync(json); }

using (var manifest = JsonDocument.Parse(JsonSerializer.Serialize(new
{
    versions = Enumerable.Range(0, 30).Select(i => new { id = "release-" + i, type = "release", releaseTime = "2026-01-01T00:00:00Z" })
        .Concat(new[] { new { id = "fixture-snapshot", type = "snapshot", releaseTime = "2026-01-02T00:00:00Z" }, new { id = "fixture-beta", type = "old_beta", releaseTime = "2011-01-01T00:00:00Z" } })
})))
{
    var entries = JsonSerializer.SerializeToElement(ResourceCommands.ManifestEntries(manifest.RootElement));
    Check(entries.GetArrayLength() == 32 && entries.EnumerateArray().Any(v => v.GetProperty("type").GetString() == "snapshot")
        && entries.EnumerateArray().Any(v => v.GetProperty("type").GetString() == "old_beta"), "vanilla manifest preserves snapshots, historical types and releases beyond the old 24-item cap");
}

await Request("""{"type":"requestState"}""");
Check(Has("state") && messages[0].GetProperty("selectedVersion").GetString() == "custom-fabric", "state reflects fixture instance");
await Request("""{"type":"saveSettings","player":"Changed","ram":1,"width":99999,"height":1}""");
Check(Has("settingsSaved") && store.Load().Player == "Changed" && store.Load().Ram == 512 && store.Load().Width == 7680, "settings persist and clamp values");
await Request("""{"type":"saveSettings","java":"fixture-java"}""");
Check(store.Load().Ram == 512 && store.Load().Width == 7680 && store.Load().Java == "fixture-java", "Java-only save preserves unrelated settings");
var secondRoot = Path.Combine(fixture, "second-root");
Directory.CreateDirectory(secondRoot);
await Request(JsonSerializer.Serialize(new { type = "saveSettings", root = secondRoot }));
Check(store.Load().GameRoots.Contains(gameRoot) && store.Load().GameRoots.Contains(secondRoot), "directory selection preserves both configured roots");
await Request(JsonSerializer.Serialize(new { type = "saveSettings", root = gameRoot }));
Check(store.Load().GameRoots.Count == 2, "directory selection deduplicates history");
await Request(JsonSerializer.Serialize(new { type = "saveSettings", root = Path.Combine(fixture, "missing"), player = "MustNotPersist" }));
Check(Has("error") && store.Load().Player == "Changed" && application.Config.Player == "Changed", "invalid directory rejected before any settings mutate");
await Request("""{"type":"saveSettings","player":"MustNotPersist","anonymous":"bad"}""");
Check(Has("error") && application.Config.Player == "Changed", "malformed settings rejected before mutation");
foreach (var bad in new[] { "[]", "{", "{}", "{\"type\":\"unknown\"}" })
{
    await Request(bad);
    Check(Has("error"), "invalid protocol has observable error: " + bad);
}
await Request("""{"type":"window","action":"minimize"}""");
Check(desktop.LastAction == "minimize", "native command routes through desktop boundary");
await Request("""{"type":"saveServer","name":"Fixture","address":"localhost:25565"}""");
Check(Has("serverResult") && store.Load().Servers.Any(s => s.Name == "Fixture"), "server save persists");
await Request("""{"type":"deleteServer","address":"localhost:25565"}""");
Check(!store.Load().Servers.Any(s => s.Name == "Fixture"), "server delete persists");
await Request("""{"type":"saveAnnouncements","items":[{"title":"fixture"}]}""");
Check(Has("community") && File.Exists(Path.Combine(fixture, "announcements.local.json")), "community local write and response");
var announcementFile = Path.Combine(fixture, "announcements.local.json");
Check(AtomicFile.ReadJson<JsonElement>(announcementFile)[0].GetProperty("title").GetString() == "fixture",
    "community persistence remains an array after atomic write");
await Request("""{"type":"publish","title":"Appended","body":"Fixture body"}""");
Check(AtomicFile.ReadJson<JsonElement>(announcementFile).GetArrayLength() == 2,
    "publishing appends to existing local announcements");
Check(Directory.GetFiles(fixture, "*.tmp").Length == 0, "atomic config and community writes leave no temporary files");

var modDir = Path.Combine(versionDir, "mods");
Directory.CreateDirectory(modDir);
File.WriteAllText(Path.Combine(modDir, "fixture.jar"), "fixture");
await Request("""{"type":"toggleMod","file":"fixture.jar","enabled":false}""");
Check(File.Exists(Path.Combine(modDir, "fixture.jar.disabled")), "mod toggle routes to selected instance");
await Request("""{"type":"launch","version":".."}""");
Check(Has("launchResult") && !messages.Last().GetProperty("ok").GetBoolean(), "instance traversal rejected with launch failure response");
application.Config.AccountType = "microsoft";
await Request("""{"type":"launch","version":"custom-fabric"}""");
Check(Has("launchResult") && messages.Last().GetProperty("ok").GetBoolean() == false, "unverified online account cannot fall back to offline launch");
application.Config.AccountType = "offline";
await Request("""{"type":"saveAnnouncements","items":"bad"}""");
Check(Has("error") && File.ReadAllText(Path.Combine(fixture, "announcements.local.json")).Contains("fixture"), "malformed community request cannot erase saved announcements");
Check(ProtocolValues.OfflineUuid("Notch") == "b50ad385829d3141a2167e7d7539ba7f", "offline UUID uses Java byte order");

var preferencesDirectory = Path.Combine(fixture, "preferences-roundtrip");
var preferencesStore = new ConfigStore(preferencesDirectory);
var savedPreferences = new MCUGLauncher.Preferences
{
    ProtectSaves = false,
    SaveBackupCount = 12,
    BackgroundPath = "fixture-background"
};
savedPreferences.Network.LimitKibPerSecond = 777;
savedPreferences.Launch.JvmArguments = "-Dfixture=true";
preferencesStore.Save(new MCUGLauncher.Models.AppConfig { Root = gameRoot, LegacyPreferences = savedPreferences });
var restoredPreferences = preferencesStore.Load().LegacyPreferences;
Check(restoredPreferences.Network.LimitKibPerSecond == 777 && restoredPreferences.SaveBackupCount == 12
    && restoredPreferences.Launch.JvmArguments == "-Dfixture=true" && restoredPreferences.BackgroundPath == "fixture-background",
    "current configuration preserves network launch appearance and save preferences");
await Request("""{"type":"toggleBookmark","id":"fixture-project","kind":"mod","name":"Fixture","description":"fixture","author":"fixture"}""");
Check(new MCUGLauncher.ResourceBookmarkStore(fixture).Read().Any(b => b.Project.Id == "fixture-project"), "bookmark command persists through Core store");
await Request("""{"type":"toggleBookmark","id":"fixture-project"}""");
Check(!new MCUGLauncher.ResourceBookmarkStore(fixture).Read().Any(b => b.Project.Id == "fixture-project"), "bookmark command removes through Core store");
await Request("""{"type":"listSaveBackups"}""");
Check(Has("saveBackups") && messages.Last().GetProperty("items").GetArrayLength() == 0, "save backup listing uses selected instance");
await Request("""{"type":"requestGames"}""");
Check(Has("games") && messages.Last().GetProperty("items").GetArrayLength() == 0, "running games request contains only owned sessions");
application.RememberWindow(1400, 900, true);
Check(store.Load().WindowWidth == 1400 && store.Load().WindowMaximized, "window dimensions persist through application boundary");
await Request("""{"type":"readImage","url":"http://localhost/private.png","requestId":"blocked-image"}""");
Check(Has("imageData") && !messages.Last().GetProperty("ok").GetBoolean(), "native image reader rejects non-HTTPS and unapproved hosts without fetching");
var emptyRoot = Path.Combine(fixture, "empty-game");
Directory.CreateDirectory(emptyRoot);
var emptyCatalog = VersionCatalog.Load(emptyRoot, "missing");
Check(emptyCatalog.Instances.Count == 0 && emptyCatalog.Selected.Length == 0 && VersionCatalog.ReadMods(emptyRoot, "").Count == 0, "empty game directory has no fake selectable instance");
using (var emptyHttp = new HttpClient())
{
    var feeds = new CommunityService(emptyHttp);
    Check(await feeds.ReadAsync("", default) == null && await feeds.ReadAsync("not-a-url", default) == null, "blank and invalid community URLs do not throw or send network requests");
}
await ResourceScenarios.Run(fixture);
await QueueScenarios.Run(fixture);
await AccountModeScenarios.Run(fixture);
await CustomizationScenarios.Run(fixture);
await SettingsScenarios.Run(fixture);
await LocalImportScenarios.Run(fixture);
await ServerPingScenarios.Run();
ArchitectureScenarios.Run(fixture);
await MaintenanceScenarios.Run(fixture);
Console.WriteLine("ALL_WEB_BRIDGE_CHECKS_PASSED (isolated fixtures; no live account or gameplay)");

sealed class FakeDesktop : IDesktopHost
{
    public Task<bool> ConfirmStopGameAsync() => Task.FromResult(false);
    public Task<bool> ConfirmUninstallAsync(string name) => Task.FromResult(false);
    public Task<string?> PickSaveFileAsync(string target) => Task.FromResult<string?>(null);
    public string LastAction = "";
    public void Window(string action) => LastAction = action;
    public void Drag()
    {
    }
    public Task<string?> PickFolderAsync(string target) => Task.FromResult<string?>(null);
    public Task<string?> PickFileAsync(string target) => Task.FromResult<string?>(null);
    public void ResetWindow()
    {
    }
}
