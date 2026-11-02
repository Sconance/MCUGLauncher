using System.Text.Json;
using BlockLaunch;
using MCUGLauncher.Contracts;
using MCUGLauncher.Models;
using MCUGLauncher.Services;
using static MCUGLauncher.UseCases.ProtocolValues;

namespace MCUGLauncher.UseCases;

internal sealed class LauncherApplication : IDisposable
{
    readonly LauncherContext context;
    Task? startup;
    readonly IDesktopHost desktop;
    readonly InstanceMaintenanceCommands maintenanceCommands;
    readonly UninstallCommands uninstallCommands;
    readonly ContentUpdateCommands updateCommands;
    readonly SaveCommands saveCommands;
    readonly BookmarkCommands bookmarkCommands;
    readonly InstallationCommands installationCommands;
    readonly AccountCommands accountCommands;
    readonly AccountModeCommands accountModes;
    readonly GameCommands gameCommands;
    readonly InstanceCommands instanceCommands;
    readonly ResourceCommands resourceCommands;
    readonly ResourceChoiceCommands resourceChoices;
    readonly LaunchOptionsCommands launchOptions;
    readonly AvatarCommands avatars;
    readonly SettingsCommands settingsCommands;
    readonly DiagnosticCommands diagnosticCommands;
    readonly CommunityCommands communityCommands;

    public AppConfig Config => context.Config;
    public LauncherApplication(Action<object> send, IDesktopHost desktop, ConfigStore? store = null, Func<string, XboxAuthNet.OAuth.CodeFlow.IWebUI>? microsoftBrowser = null, Func<byte[]?, string>? avatarEncoder = null)
    {
        this.desktop = desktop;
        context = new LauncherContext(send, store, microsoftBrowser);
        context.RefreshState = SendState;
        maintenanceCommands = new InstanceMaintenanceCommands(context);
        uninstallCommands = new UninstallCommands(context);
        updateCommands = new ContentUpdateCommands(context);
        saveCommands = new SaveCommands(context);
        bookmarkCommands = new BookmarkCommands(context);
        installationCommands = new InstallationCommands(context);
        accountCommands = new AccountCommands(context);
        accountModes = new AccountModeCommands(context);
        gameCommands = new GameCommands(context);
        instanceCommands = new InstanceCommands(context);
        resourceCommands = new ResourceCommands(context);
        resourceChoices = new ResourceChoiceCommands(context);
        launchOptions = new LaunchOptionsCommands(context);
        avatars = new AvatarCommands(context, avatarEncoder ?? (_ => ""));
        settingsCommands = new SettingsCommands(context);
        diagnosticCommands = new DiagnosticCommands(context);
        communityCommands = new CommunityCommands(context);
    }

    public void RememberWindow(double width, double height, bool maximized)
    {
        context.Config.WindowWidth = Math.Clamp(width, LaunchLimits.ShellWidthMin, LaunchLimits.ShellWidthMax);
        context.Config.WindowHeight = Math.Clamp(height, LaunchLimits.ShellHeightMin, LaunchLimits.ShellHeightMax);
        context.Config.WindowMaximized = maximized;
        context.Store.Save(context.Config);
    }

    public async Task HandleAsync(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("消息必须是对象。");
            ProtocolValues.ValidateMessage(root);
            var type = ReadString(root, "type");
            switch (type)
            {
                case "ready":
                    // The browser can request fresh state after every navigation.
                    context.RefreshState();
                    context.SendTasks();
                    context.SendGames();
                    context.Tasks.Start();
                    await (startup ??= Task.WhenAll(communityCommands.SendCommunityAsync(), accountModes.RestoreSelectedAsync()));
                    break;
                case "cloneInstance":
                    maintenanceCommands.Clone(root);
                    break;
                case "exportInstance":
                    maintenanceCommands.Export(root);
                    break;
                case "pickSaveFile":
                    var saveTarget = ReadString(root, "target");
                    var savePath = desktop.PickSaveFile(saveTarget);
                    if (savePath is not null)
                        context.Send(new
                        {
                            type = "pickedPath",
                            target = saveTarget,
                            path = savePath
                        });
                    break;
                case "previewUninstall":
                    await uninstallCommands.PreviewAsync(ReadString(root, "version"));
                    break;
                case "applyUninstall":
                    var uninstallToken = ReadString(root, "token");
                    if (desktop.ConfirmUninstall(uninstallCommands.Name(uninstallToken)))
                        uninstallCommands.Apply(uninstallToken);
                    break;
                case "checkContentUpdates":
                    await updateCommands.CheckAsync(ReadString(root, "version"));
                    break;
                case "previewContentUpdates":
                    await updateCommands.PreviewAsync(root);
                    break;
                case "applyContentUpdates":
                    updateCommands.Apply(ReadString(root, "token"));
                    break;
                case "listSaveBackups":
                    saveCommands.List();
                    break;
                case "restoreSave":
                    await saveCommands.RestoreAsync(root);
                    break;
                case "toggleBookmark":
                    bookmarkCommands.Toggle(root);
                    break;
                case "requestGames":
                    context.SendGames();
                    break;
                case "stopGame":
                    if (desktop.ConfirmStopGame())
                        gameCommands.Stop(ReadString(root, "id"));
                    break;
                case "listLoaders":
                    await installationCommands.LoadersAsync(root);
                    break;
                case "installLoader":
                    installationCommands.InstallLoader(root);
                    break;
                case "saveCustomSources":
                    settingsCommands.SaveCustomSources(root);
                    break;
                case "useDataSources":
                    settingsCommands.UseDataSources(ReadString(root, "mode"));
                    break;
                case "resetDataSources":
                    settingsCommands.ResetDataSources();
                    break;
                case "readAvatar":
                    await avatars.ReadAsync();
                    break;
                case "setOfflineSkin":
                    await avatars.SetAsync(root);
                    break;
                case "readLaunchOptions":
                    launchOptions.Read(root);
                    break;
                case "saveLaunchOptions":
                    launchOptions.Save(root);
                    break;
                case "selectMuaProfile":
                    accountCommands.SelectMuaProfile(root);
                    break;
                case "listContentVersions":
                    await resourceChoices.ListAsync(root);
                    break;
                case "queueContentVersion":
                    resourceChoices.Queue(root);
                    break;
                case "previewPack":
                    await installationCommands.PreviewPackAsync(root);
                    break;
                case "confirmPack":
                    installationCommands.ConfirmPack(root);
                    break;
                case "importPack":
                    await installationCommands.ImportPackAsync(root);
                    break;
                case "requestTasks":
                    context.SendTasks();
                    break;
                case "taskAction":
                    var taskId = ReadString(root, "id");
                    var taskAction = ReadString(root, "action");
                    var pending = context.Tasks.History().FirstOrDefault(t => t.Id == taskId);
                    if (taskAction == "resume" && pending?.Download?.Kind == "uninstall" && !desktop.ConfirmUninstall(pending.Name))
                        break;
                    context.Tasks.Change(taskId, taskAction);
                    break;
                case "requestState":
                    context.RefreshState();
                    break;
                case "refreshCommunity":
                    await communityCommands.SendCommunityAsync(notify: true);
                    break;
                case "launch":
                    await gameCommands.LaunchAsync(ReadString(root, "version"));
                    break;
                case "window":
                    desktop.Window(ReadString(root, "action"));
                    break;
                case "drag":
                    desktop.Drag();
                    break;
                case "selectVersion":
                    context.Config.LastVersion = ReadString(root, "version");
                    context.Config.LastInstance = context.Config.LastVersion;
                    context.Store.Save(context.Config);
                    context.RefreshState();
                    break;
                case "selectInstance":
                    context.Config.LastInstance = ReadString(root, "instance");
                    context.Store.Save(context.Config);
                    context.RefreshState();
                    break;
                case "openFolder":
                    instanceCommands.OpenFolder(ReadString(root, "kind"));
                    break;
                case "pingServer":
                    await communityCommands.PingAsync(ReadString(root, "address"), ReadString(root, "id"));
                    break;
                case "saveServer":
                    communityCommands.SaveServer(root);
                    break;
                case "deleteServer":
                    communityCommands.DeleteServer(ReadString(root, "address"));
                    break;
                case "toggleMod":
                    instanceCommands.ToggleMod(ReadString(root, "file"), root.TryGetProperty("enabled", out var enabled) && enabled.GetBoolean());
                    break;
                case "deleteMod":
                    instanceCommands.DeleteMod(ReadString(root, "file"));
                    break;
                case "deleteVersion":
                    await uninstallCommands.PreviewAsync(ReadString(root, "version"));
                    break;
                case "searchContent":
                    context.Log("searchContent " + ReadString(root, "category") + " " + ReadString(root, "query"));
                    await resourceCommands.SearchContentAsync(root);
                    break;
                case "installContent":
                    await resourceCommands.InstallContentAsync(root);
                    break;
                case "vanillaVersions":
                    await resourceCommands.SendVanillaVersionsAsync();
                    break;
                case "installVanilla":
                    await resourceCommands.InstallVanillaAsync(ReadString(root, "version"));
                    break;
                case "installMod":
                    await resourceCommands.InstallModAsync(root);
                    break;
                case "saveSettings":
                    settingsCommands.SaveSettings(root);
                    break;
                case "pickFolder":
                    var folderTarget = ReadString(root, "target");
                    var folderPath = desktop.PickFolder(folderTarget);
                    if (folderPath is not null)
                        context.Send(new
                        {
                            type = "pickedPath",
                            target = folderTarget,
                            path = folderPath
                        });
                    break;
                case "pickFile":
                    var fileTarget = ReadString(root, "target");
                    var filePath = desktop.PickFile(fileTarget);
                    if (filePath is not null)
                        context.Send(new
                        {
                            type = "pickedPath",
                            target = fileTarget,
                            path = filePath
                        });
                    break;
                case "scanJava":
                    await settingsCommands.ScanJavaAsync();
                    break;
                case "backupSaves":
                    await saveCommands.BackupAsync();
                    break;
                case "readLog":
                    diagnosticCommands.ReadLog();
                    break;
                case "systemInfo":
                    diagnosticCommands.SendSystemInfo();
                    break;
                case "resetWindow":
                    desktop.ResetWindow();
                    break;
                case "loginMicrosoft":
                    await accountCommands.LoginMicrosoftAsync();
                    break;
                case "loginMua":
                    await accountCommands.LoginMuaAsync(ReadString(root, "account"), ReadString(root, "password"));
                    break;
                case "logoutAccount":
                    await accountModes.SwitchAsync("offline");
                    break;
                case "switchAccountMode":
                    await accountModes.SwitchAsync(ReadString(root, "account"));
                    break;
                case "forgetAccount":
                    await accountModes.ForgetAsync(ReadString(root, "account"));
                    break;
                case "testSource":
                    await communityCommands.TestSourceAsync(ReadString(root, "name"), ReadString(root, "url"), ReadString(root, "requestId"));
                    break;
                case "verifyKey":
                    communityCommands.VerifyKey(ReadString(root, "role"), ReadString(root, "key"));
                    break;
                case "publish":
                    await communityCommands.PublishAsync(root);
                    break;
                case "saveAnnouncements":
                    communityCommands.SaveCommunityLocal("announcements.local.json", root, "items");
                    break;
                case "saveRecommendations":
                    communityCommands.SaveCommunityLocal("recommendations.local.json", root, "items");
                    break;
                default:
                    throw new ArgumentException("不支持的消息类型：" + type);
            }
        }
        catch (Exception ex)
        {
            context.Log(ex.ToString());
            context.Send(new
            {
                type = "error",
                message = ex.Message
            });
        }
    }

    // Build the snapshot away from the UI thread; LauncherService marshals the typed result.
    public void SendState()
    {
        try
        {
            var state = LauncherStateBuilder.Build(context.Config, bookmarkCommands.Snapshot());
            context.Send(state);
        }
        catch (Exception ex)
        {
            context.Log(ex.ToString());
            context.Send(new
            {
                type = "error",
                message = ex.Message
            });
        }
    }

    // Bounded image fetch for community announcements; the facade only receives bytes.
    public async Task<byte[]> DownloadImageAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
            || uri.UserInfo.Length > 0)
            throw new ArgumentException("图片地址无效。");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        using var response = await context.Http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > 4 * 1024 * 1024)
            throw new InvalidDataException("公告配图不能超过 4 MB。");
        await using var input = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await input.ReadAsync(buffer, timeout.Token)) > 0)
        {
            if (output.Length + read > 4 * 1024 * 1024)
                throw new InvalidDataException("公告配图不能超过 4 MB。");
            await output.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
        }
        return output.ToArray();
    }

    public void Dispose() => context.Dispose();
}
