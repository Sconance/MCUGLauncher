using System.IO;
using System.Net.Http;
using System.Net;
using System.Text.Json;
using System.Text;
using System.Reflection;
using MCUGLauncher.Contracts;
using MCUGLauncher.Services;
using MCUGLauncher.UseCases;
using MCUGLauncher;

var fixture = Path.Combine(Path.GetTempPath(), "mcug-launcher-check-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(fixture);
var store = new ConfigStore(fixture);
var gameRoot = Path.Combine(fixture, "game");
var versionDir = Path.Combine(gameRoot, "versions", "custom-fabric");
Directory.CreateDirectory(versionDir);
File.WriteAllText(Path.Combine(versionDir, "custom-fabric.json"), """{"id":"custom-fabric","inheritsFrom":"1.21.1"}""");
store.Save(new MCUGLauncher.Models.AppConfig { Root = gameRoot, Player = "Fixture", LastVersion = "custom-fabric", LastInstance = "custom-fabric" });
var messages = new List<JsonElement>();
var desktop = new FakeDesktop();
var wire = new JsonSerializerOptions(JsonSerializerDefaults.Web);
using var application = new LauncherApplication(value => messages.Add(JsonSerializer.SerializeToElement(value, wire)), desktop, store);
bool Has(string type) => messages.Any(m =>
    m.TryGetProperty("type", out var messageType) && messageType.GetString() == type);
void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); }
async Task Request(string json) { messages.Clear(); await application.HandleAsync(json); }

await Request("""{"type":"requestState"}""");
Check(messages.Count > 0 && messages[0].GetProperty("selectedVersion").GetString() == "custom-fabric", "state reflects fixture instance");
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
var markdownAnnouncement = CommunityService.ParseMarkdownAnnouncement(
    """
    # tags
    更新, 社区

    # preview_body
    预览正文第一行。
    预览正文第二行。

    # author
    测试作者

    # preview_title
    测试公告

    # 完整公告
    正文保留。
    """,
    "https://example.test/announcement.md");
Check(markdownAnnouncement.Title == "测试公告"
    && markdownAnnouncement.Body == "预览正文第一行。\n预览正文第二行。"
    && markdownAnnouncement.Tag == "更新 · 社区"
    && markdownAnnouncement.Author == "测试作者"
    && markdownAnnouncement.Markdown.StartsWith("# 完整公告", StringComparison.Ordinal),
    "markdown announcement parses preview fields and preserves full article");
var emptyMarkdownAnnouncement = CommunityService.ParseMarkdownAnnouncement(
    "# 完整公告\n只显示在全文中的内容。",
    "https://example.test/empty.md");
Check(emptyMarkdownAnnouncement.Body == "一份很懒的公告"
    && emptyMarkdownAnnouncement.Markdown.Contains("只显示在全文中的内容。", StringComparison.Ordinal),
    "markdown announcement applies empty-preview fallback");
var partialMarkdownAnnouncement = CommunityService.ParseMarkdownAnnouncement(
    "# preview_title\n只有标题",
    "https://example.test/partial.md");
Check(partialMarkdownAnnouncement.Title == "只有标题" && partialMarkdownAnnouncement.Body.Length == 0,
    "markdown announcement only applies fallback when every preview field is empty");
var codeMarkdownAnnouncement = CommunityService.ParseMarkdownAnnouncement(
    "```markdown\n# preview_title\n示例文本\n```\n",
    "https://example.test/code.md");
Check(codeMarkdownAnnouncement.Title.Length == 0
    && codeMarkdownAnnouncement.Markdown.Contains("# preview_title", StringComparison.Ordinal),
    "markdown announcement ignores metadata-like headings inside code fences");
const string missingMarkdownAnnouncementsUrl =
    "https://raw.githubusercontent.com/waibibabu1239/MCUGL-storage/main/announcements.md";
const string unpublishedMainAnnouncementsUrl =
    "https://raw.githubusercontent.com/Sconance/MCUGLauncher/main/community/announcements.md";
var missingMarkdownStore = new ConfigStore(Path.Combine(fixture, "missing-markdown-announcements"));
missingMarkdownStore.Save(new MCUGLauncher.Models.AppConfig
{
    DataSources = new MCUGLauncher.Models.DataSourceConfig { Announcements = missingMarkdownAnnouncementsUrl },
    DataSourceMode = "default"
});
Check(missingMarkdownStore.Load().DataSources.Announcements == new MCUGLauncher.Models.DataSourceConfig().Announcements,
    "missing default Markdown announcement source migrates to the published source");
var unpublishedMainStore = new ConfigStore(Path.Combine(fixture, "unpublished-main-announcements"));
unpublishedMainStore.Save(new MCUGLauncher.Models.AppConfig
{
    DataSources = new MCUGLauncher.Models.DataSourceConfig { Announcements = unpublishedMainAnnouncementsUrl },
    DataSourceMode = "default"
});
Check(unpublishedMainStore.Load().DataSources.Announcements == new MCUGLauncher.Models.DataSourceConfig().Announcements,
    "unpublished main announcement source migrates to the published branch");
const string customAnnouncementsUrl = "https://example.test/custom-announcements.md";
var customAnnouncementStore = new ConfigStore(Path.Combine(fixture, "custom-markdown-announcements"));
customAnnouncementStore.Save(new MCUGLauncher.Models.AppConfig
{
    DataSources = new MCUGLauncher.Models.DataSourceConfig { Announcements = customAnnouncementsUrl },
    DataSourceMode = "custom"
});
Check(customAnnouncementStore.Load().DataSources.Announcements == customAnnouncementsUrl,
    "custom Markdown announcement source is preserved during migration");
try
{
    _ = CommunityService.ParseMarkdownAnnouncement(
        "# preview_title\n一\n# preview_title\n二",
        "https://example.test/duplicate.md");
    Check(false, "duplicate markdown preview fields are rejected");
}
catch (InvalidDataException)
{
    Check(true, "duplicate markdown preview fields are rejected");
}
try
{
    _ = CommunityService.ParseMarkdownAnnouncement(
        """[{"title":"legacy JSON announcement"}]""",
        "https://example.test/announcements.json");
    Check(false, "JSON announcement lists are rejected");
}
catch (InvalidDataException)
{
    Check(true, "JSON announcement lists are rejected");
}
using (var markdownClient = new HttpClient(new MarkdownResponseHandler(
           "# preview_title\n网络公告\n\n# 正文\n从 Markdown URL 读取。")))
{
    var markdownService = new CommunityService(markdownClient);
    var loaded = await markdownService.ReadAnnouncementAsync(
        "https://example.test/announcement.md",
        CancellationToken.None);
    var tested = await markdownService.TestAnnouncementAsync(
        "https://example.test/announcement.md",
        CancellationToken.None);
    Check(loaded.Ok
        && loaded.Markdown.Contains("# preview_title", StringComparison.Ordinal)
        && tested.Ok
        && tested.Text.Contains("Markdown 公告有效", StringComparison.Ordinal),
        "announcement service loads and validates a direct Markdown URL");
}
await Request("""{"type":"window","action":"minimize"}""");
Check(desktop.LastAction == "minimize", "native command routes through desktop boundary");
await Request("""{"type":"saveServer","name":"Fixture","address":"localhost:25565"}""");
Check(Has("serverResult") && store.Load().Servers.Any(s => s.Name == "Fixture"), "server save persists");
await Request("""{"type":"deleteServer","address":"localhost:25565"}""");
Check(!store.Load().Servers.Any(s => s.Name == "Fixture"), "server delete persists");
await Request("""{"type":"saveRecommendations","items":[{"name":"fixture"}]}""");
Check(Has("community") && File.Exists(Path.Combine(fixture, "recommendations.local.json")), "recommendation local write and response");
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
Check(Has("error") && File.ReadAllText(Path.Combine(fixture, "recommendations.local.json")).Contains("fixture"), "removed announcement management protocol is rejected");
Check(ProtocolValues.OfflineUuid("Notch") == "b50ad385829d3141a2167e7d7539ba7f", "offline UUID uses Java byte order");

var legacyDirectory = Path.Combine(fixture, "legacy");
Directory.CreateDirectory(legacyDirectory);
var legacyPreferences = new BlockLaunch.Preferences
{
    Player = "Old",
    ProtectSaves = false,
    SaveBackupCount = 12,
    ExtraJavaPaths = new List<string> { "fixture-java" },
    BackgroundPath = "fixture-background"
};
legacyPreferences.Network.LimitKibPerSecond = 777;
legacyPreferences.Launch.JvmArguments = "-Dfixture=true";
new BlockLaunch.SettingsStore(legacyDirectory).Save(legacyPreferences);
var imported = ConfigStore.ReadLegacy(Path.Combine(legacyDirectory, "settings.json"))!;
Check(imported.LegacyPreferences.Network.LimitKibPerSecond == 777 && imported.LegacyPreferences.SaveBackupCount == 12
    && imported.LegacyPreferences.Launch.JvmArguments == "-Dfixture=true" && imported.LegacyPreferences.BackgroundPath == "fixture-background",
    "legacy migration preserves network launch appearance and save preferences");
await Request("""{"type":"toggleBookmark","id":"fixture-project","kind":"mod","name":"Fixture","description":"fixture","author":"fixture"}""");
Check(new BlockLaunch.ResourceBookmarkStore(fixture).Read().Any(b => b.Project.Id == "fixture-project"), "bookmark command persists through Core store");
await Request("""{"type":"toggleBookmark","id":"fixture-project"}""");
Check(!new BlockLaunch.ResourceBookmarkStore(fixture).Read().Any(b => b.Project.Id == "fixture-project"), "bookmark command removes through Core store");
await Request("""{"type":"listSaveBackups"}""");
Check(Has("saveBackups") && messages.Last().GetProperty("items").GetArrayLength() == 0, "save backup listing uses selected instance");
await Request("""{"type":"requestGames"}""");
Check(Has("games") && messages.Last().GetProperty("items").GetArrayLength() == 0, "running games request contains only owned sessions");
application.RememberWindow(1400, 900, true);
Check(store.Load().WindowWidth == 1400 && store.Load().WindowMaximized, "window dimensions persist through application boundary");
await ResourceScenarios.Run(fixture);
await QueueScenarios.Run(fixture);
await AccountModeScenarios.Run(fixture);
await CustomizationScenarios.Run(fixture);
await MaintenanceScenarios.Run(fixture);
using (var service = new LauncherService(
           desktop,
           action => action(),
           _ => null!,
           _ => "",
           store))
{
    var featureOperations = typeof(ILauncherService).GetMethods()
        .Where(method => method.IsDefined(typeof(FeatureOperationAttribute)))
        .ToArray();
    var featureHandlers = typeof(LauncherService).Assembly.GetTypes()
        .Select(type => (Type: type, Registration: type.GetCustomAttribute<FeatureHandlerAttribute>()))
        .Where(candidate => candidate.Registration is not null)
        .ToArray();
    Check(featureOperations.Length > 0
        && featureOperations.All(operation =>
            featureHandlers.Count(handler => handler.Registration!.Operation == operation.Name) == 1
            && featureHandlers.Single(handler => handler.Registration!.Operation == operation.Name)
                .Type.GetInterfaces()
                .Single(contract => contract.IsGenericType
                    && contract.GetGenericTypeDefinition().Name == "IFeatureHandler`1")
                .GetGenericArguments()[0] == operation.ReturnType.GetGenericArguments()[0]),
        "feature handlers and result types are discovered from service declarations");

    LauncherState? typedState = null;
    service.StateChanged += state => typedState = state;
    await service.RequestStateAsync();
    Check(typedState?.SelectedVersion == "custom-fabric" && service.State == typedState,
        "typed launcher service exposes state without the UI JSON protocol");
    Check(service.WindowPlacement.Width == 1400 && service.WindowPlacement.Maximized,
        "typed launcher service exposes immutable window placement");
    ILauncherService serviceContract = service;
    var sourceResult = await serviceContract.TestSourceAsync("Fixture", "ftp://invalid");
    Check(sourceResult.Name == "Fixture"
        && !sourceResult.Ok
        && sourceResult.Message.Contains("https://", StringComparison.Ordinal),
        "typed feature call returns its declared result without an event protocol");
    var accountSwitch = await serviceContract.SwitchAccountModeAsync("offline");
    Check(accountSwitch.Account == "offline" && accountSwitch.Message.Contains("离线", StringComparison.Ordinal),
        "typed account operation returns its declared result");
    var loginResult = await serviceContract.LoginMuaAsync("", "");
    Check(loginResult.Ok == false && loginResult.Message.Length > 0,
        "typed account login returns validation details");
    var savedServer = await serviceContract.SaveServerAsync("Typed fixture", "typed.example:25565");
    Check(savedServer.Ok && store.Load().Servers.Any(server => server.Address == "typed.example:25565"),
        "typed community operation returns its declared result");
    var invalidKey = await serviceContract.VerifyKeyAsync("admin", "short");
    Check(!invalidKey.Ok && invalidKey.Role == "admin",
        "typed key verification returns validation details");
    var settingsResult = await serviceContract.SaveSettingsAsync(new AppSettingsPatch(Java: "typed-java"));
    Check(settingsResult.Message.Length > 0 && store.Load().Java == "typed-java",
        "typed settings operation returns its declared result");
    var sourcesResult = await serviceContract.SaveCustomSourcesAsync(new DataSourceSet("", "", "", ""));
    Check(sourcesResult.Message.Length > 0,
        "typed data-source operation returns its declared result");
}
Console.WriteLine("ALL_LAUNCHER_CHECKS_PASSED (isolated fixtures; no live account or gameplay)");

sealed class FakeDesktop : IDesktopHost
{
    public bool ConfirmStopGame() => false;
    public bool ConfirmUninstall(string name) => false;
    public string? PickSaveFile(string target) => null;
    public string LastAction = "";
    public void Window(string action) => LastAction = action;
    public void Drag()
    {
    }
    public string? PickFolder(string target) => null;
    public string? PickFile(string target) => null;
    public void ResetWindow()
    {
    }
}

sealed class MarkdownResponseHandler(string markdown) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(markdown, Encoding.UTF8, "text/markdown")
        });
}
