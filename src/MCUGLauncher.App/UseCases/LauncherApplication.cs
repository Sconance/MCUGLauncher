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
    readonly FeatureDispatcher featureDispatcher;

    public AppConfig Config => context.Config;
    public LauncherApplication(Action<object> send, IDesktopHost desktop, ConfigStore? store = null, Func<string, XboxAuthNet.OAuth.CodeFlow.IWebUI>? microsoftBrowser = null, Func<byte[]?, string>? avatarEncoder = null)
    {
        this.desktop = desktop;
        context = new LauncherContext(send, store, microsoftBrowser, avatarEncoder);
        context.RefreshState = SendState;
        maintenanceCommands = new InstanceMaintenanceCommands(context);
        uninstallCommands = new UninstallCommands(context);
        updateCommands = new ContentUpdateCommands(context);
        saveCommands = new SaveCommands(context);
        bookmarkCommands = new BookmarkCommands(context);
        installationCommands = context.Installations;
        accountCommands = new AccountCommands(context);
        accountModes = new AccountModeCommands(context);
        gameCommands = new GameCommands(context);
        instanceCommands = new InstanceCommands(context);
        resourceCommands = new ResourceCommands(context);
        resourceChoices = new ResourceChoiceCommands(context);
        launchOptions = new LaunchOptionsCommands(context);
        avatars = context.Avatars;
        settingsCommands = new SettingsCommands(context);
        diagnosticCommands = new DiagnosticCommands(context);
        communityCommands = new CommunityCommands(context);
        featureDispatcher = new FeatureDispatcher(context);
    }

    public Task<TResponse> ExecuteFeatureAsync<TResponse>(string operation, object arguments) =>
        featureDispatcher.ExecuteAsync<TResponse>(operation, arguments);

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
                    await accountModes.PreloadIdentitiesAsync();
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
                    var uninstallPreview = await uninstallCommands.PreviewAsync(ReadString(root, "version"));
                    context.Send(new
                    {
                        type = "uninstallPreview",
                        token = uninstallPreview.Token,
                        version = uninstallPreview.Version,
                        name = uninstallPreview.Name,
                        paths = uninstallPreview.Paths,
                        shared = uninstallPreview.Shared,
                        notice = uninstallPreview.Notice
                    });
                    break;
                case "applyUninstall":
                    var uninstallToken = ReadString(root, "token");
                    if (desktop.ConfirmUninstall(uninstallCommands.Name(uninstallToken)))
                        uninstallCommands.Apply(uninstallToken);
                    break;
                case "checkContentUpdates":
                    var updates = await updateCommands.CheckAsync(ReadString(root, "version"));
                    context.Send(new
                    {
                        type = "contentUpdates",
                        token = updates.Token,
                        version = updates.Version,
                        items = updates.Items.Select(item => new
                        {
                            id = item.Id,
                            name = item.Name,
                            before = item.Before,
                            after = item.After,
                            available = item.Available,
                            error = item.Error
                        })
                    });
                    break;
                case "previewContentUpdates":
                    var updatePreview = await updateCommands.PreviewAsync(root);
                    context.Send(new
                    {
                        type = "contentUpdatePreview",
                        token = updatePreview.Token,
                        version = updatePreview.Version,
                        bytes = updatePreview.Bytes,
                        changes = updatePreview.Changes
                    });
                    break;
                case "applyContentUpdates":
                    updateCommands.Apply(ReadString(root, "token"));
                    break;
                case "listSaveBackups":
                    var listedBackups = saveCommands.List();
                    context.Send(new
                    {
                        type = "saveBackups",
                        items = listedBackups.Select(item => new { id = item.Id, created = item.Created, worlds = item.Worlds })
                    });
                    break;
                case "restoreSave":
                    var restoredSave = await saveCommands.RestoreAsync(root);
                    context.Send(new
                    {
                        type = "toast",
                        message = restoredSave.Message
                    });
                    break;
                case "toggleBookmark":
                    context.Send(new
                    {
                        type = "saved",
                        message = bookmarkCommands.Toggle(root).Message
                    });
                    break;
                case "requestGames":
                    context.SendGames();
                    break;
                case "stopGame":
                    if (desktop.ConfirmStopGame())
                        gameCommands.Stop(ReadString(root, "id"));
                    break;
                case "listLoaders":
                    var loaderList = await installationCommands.LoadersAsync(root);
                    if (loaderList.Ok && loaderList.Result is { } loaders)
                        context.Send(new
                        {
                            type = "loaderList",
                            version = loaders.Version,
                            loader = loaders.Loader,
                            versions = loaders.Versions
                        });
                    else
                        context.Send(new
                        {
                            type = "error",
                            message = loaderList.Error
                        });
                    break;
                case "installLoader":
                    installationCommands.InstallLoader(root);
                    break;
                case "saveCustomSources":
                    settingsCommands.SaveCustomSources(root);
                    context.Send(new
                    {
                        type = "settingsSaved"
                    });
                    break;
                case "useDataSources":
                    settingsCommands.UseDataSources(ReadString(root, "mode"));
                    await communityCommands.SendCommunityAsync(notify: true);
                    break;
                case "resetDataSources":
                    settingsCommands.ResetDataSources();
                    await communityCommands.SendCommunityAsync(notify: true);
                    break;
                case "readAvatar":
                    var avatar = await avatars.ReadAsync();
                    if (avatar is not null)
                        context.Send(new
                        {
                            type = "avatar",
                            image = avatar.Image,
                            label = avatar.Label,
                            fallback = avatar.Fallback,
                            account = avatar.Account,
                            player = avatar.Player
                        });
                    break;
                case "setOfflineSkin":
                    var skinAvatar = await avatars.SetAsync(root);
                    if (skinAvatar is not null)
                        context.Send(new
                        {
                            type = "avatar",
                            image = skinAvatar.Image,
                            label = skinAvatar.Label,
                            fallback = skinAvatar.Fallback,
                            account = skinAvatar.Account,
                            player = skinAvatar.Player
                        });
                    break;
                case "readLaunchOptions":
                    var readOptions = launchOptions.Read(root);
                    context.Send(new
                    {
                        type = "launchOptions",
                        requestId = readOptions.RequestId,
                        token = readOptions.Token,
                        version = readOptions.Version,
                        name = readOptions.Name,
                        inherited = readOptions.Inherited,
                        settings = readOptions.Settings
                    });
                    break;
                case "saveLaunchOptions":
                    var launchSaveResult = launchOptions.Save(root);
                    context.Send(new
                    {
                        type = "launchOptionsSaved",
                        message = launchSaveResult.Message
                    });
                    break;
                case "selectMuaProfile":
                    accountCommands.SelectMuaProfile(root);
                    break;
                case "listContentVersions":
                    var resourceVersions = await resourceChoices.ListAsync(root);
                    if (resourceVersions.Ok && resourceVersions.Result is { } listedVersions)
                        context.Send(new
                        {
                            type = "resourceVersions",
                            requestId = listedVersions.RequestId,
                            token = listedVersions.Token,
                            game = listedVersions.Game,
                            loader = listedVersions.Loader,
                            items = listedVersions.Items
                        });
                    else
                        context.Send(new
                        {
                            type = "contentError",
                            category = ReadString(root, "category"),
                            requestId = ReadString(root, "requestId"),
                            message = resourceVersions.Error
                        });
                    break;
                case "queueContentVersion":
                    resourceChoices.Queue(root);
                    break;
                case "previewPack":
                    var packPreview = await installationCommands.PreviewPackAsync(root);
                    if (packPreview.Ok && packPreview.Preview is { } preview)
                        context.Send(new
                        {
                            type = "packPreview",
                            token = preview.Token,
                            name = preview.Name,
                            root = preview.Root,
                            game = preview.Game,
                            loader = preview.Loader
                        });
                    else
                        context.Send(new
                        {
                            type = "error",
                            message = packPreview.Error
                        });
                    break;
                case "confirmPack":
                    installationCommands.ConfirmPack(root);
                    break;
                case "importPack":
                    var importedPack = await installationCommands.ImportPackAsync(root);
                    if (importedPack.Ok && importedPack.Preview is { } importedPreview)
                        context.Send(new
                        {
                            type = "packPreview",
                            token = importedPreview.Token,
                            name = importedPreview.Name,
                            root = importedPreview.Root,
                            game = importedPreview.Game,
                            loader = importedPreview.Loader
                        });
                    else
                        context.Send(new
                        {
                            type = "error",
                            message = importedPack.Error
                        });
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
                    var launchResult = await gameCommands.LaunchAsync(ReadString(root, "version"));
                    context.Send(new
                    {
                        type = "launchResult",
                        ok = launchResult.Ok,
                        message = launchResult.Message
                    });
                    break;
                case "window":
                    desktop.Window(ReadString(root, "action"));
                    break;
                case "drag":
                    desktop.Drag();
                    break;
                case "openFolder":
                    instanceCommands.OpenFolder(ReadString(root, "kind"));
                    break;
                case "pingServer":
                    var ping = await communityCommands.PingAsync(ReadString(root, "address"), ReadString(root, "id"));
                    context.Send(new
                    {
                        type = "serverPing",
                        id = ping.Id,
                        online = ping.Online,
                        players = ping.Players,
                        max = ping.Max,
                        ping = ping.Ping,
                        motd = ping.Motd,
                        version = ping.Version,
                        error = ping.Error
                    });
                    break;
                case "saveServer":
                    var serverResult = communityCommands.SaveServer(
                        ReadString(root, "name"),
                        ReadString(root, "address"),
                        ReadString(root, "oldAddress"));
                    context.Send(new
                    {
                        type = "serverResult",
                        ok = serverResult.Ok,
                        message = serverResult.Message
                    });
                    break;
                case "deleteServer":
                    context.Send(new
                    {
                        type = "saved",
                        message = communityCommands.DeleteServer(ReadString(root, "address")).Message
                    });
                    break;
                case "toggleMod":
                    context.Send(new
                    {
                        type = "saved",
                        message = instanceCommands.ToggleMod(
                            ReadString(root, "file"),
                            root.TryGetProperty("enabled", out var enabled) && enabled.GetBoolean()).Message
                    });
                    break;
                case "deleteMod":
                    context.Send(new
                    {
                        type = "saved",
                        message = instanceCommands.DeleteMod(ReadString(root, "file")).Message
                    });
                    break;
                case "deleteVersion":
                    var versionPreview = await uninstallCommands.PreviewAsync(ReadString(root, "version"));
                    context.Send(new
                    {
                        type = "uninstallPreview",
                        token = versionPreview.Token,
                        version = versionPreview.Version,
                        name = versionPreview.Name,
                        paths = versionPreview.Paths,
                        shared = versionPreview.Shared,
                        notice = versionPreview.Notice
                    });
                    break;
                case "searchContent":
                    context.Log("searchContent " + ReadString(root, "category") + " " + ReadString(root, "query"));
                    var search = await resourceCommands.SearchContentAsync(root);
                    if (search.Ok)
                        context.Send(new
                        {
                            type = "contentList",
                            category = search.Category,
                            requestId = search.RequestId,
                            items = search.Items
                        });
                    else
                        context.Send(new
                        {
                            type = "contentError",
                            category = search.Category,
                            requestId = search.RequestId,
                            message = search.Error
                        });
                    break;
                case "installContent":
                    await resourceCommands.InstallContentAsync(root);
                    break;
                case "vanillaVersions":
                    var vanilla = await resourceCommands.SendVanillaVersionsAsync();
                    if (vanilla.Ok)
                        context.Send(new
                        {
                            type = "vanillaList",
                            items = vanilla.Items
                        });
                    else
                        context.Send(new
                        {
                            type = "contentError",
                            category = "vanilla",
                            message = vanilla.Error
                        });
                    break;
                case "installVanilla":
                    await resourceCommands.InstallVanillaAsync(ReadString(root, "version"));
                    break;
                case "installMod":
                    await resourceCommands.InstallModAsync(root);
                    break;
                case "saveSettings":
                    settingsCommands.SaveSettings(root);
                    context.Send(new
                    {
                        type = "settingsSaved"
                    });
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
                    var javaItems = await settingsCommands.ScanJavaAsync();
                    context.Send(new
                    {
                        type = "javaList",
                        items = javaItems.Select(item => new
                        {
                            path = item.Path,
                            major = item.Major,
                            arch = item.Arch,
                            vendor = item.Vendor,
                            error = item.Error
                        })
                    });
                    break;
                case "backupSaves":
                    var savedBackup = await saveCommands.BackupAsync();
                    context.Send(new
                    {
                        type = "toast",
                        message = savedBackup.Message
                    });
                    var refreshedBackups = saveCommands.List();
                    context.Send(new
                    {
                        type = "saveBackups",
                        items = refreshedBackups.Select(item => new { id = item.Id, created = item.Created, worlds = item.Worlds })
                    });
                    break;
                case "readLog":
                    context.Send(new
                    {
                        type = "logText",
                        text = diagnosticCommands.ReadLog()
                    });
                    break;
                case "systemInfo":
                    var systemInfo = diagnosticCommands.SendSystemInfo();
                    context.Send(new
                    {
                        type = "systemInfo",
                        app = systemInfo.App,
                        dotnet = systemInfo.Dotnet,
                        webview = systemInfo.Webview,
                        os = systemInfo.Os,
                        root = systemInfo.Root,
                        data = systemInfo.Data
                    });
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
                case "verifyKey":
                    var keyResult = communityCommands.VerifyKey(ReadString(root, "role"), ReadString(root, "key"));
                    context.Send(new
                    {
                        type = "keyResult",
                        role = keyResult.Role,
                        ok = keyResult.Ok,
                        message = keyResult.Message
                    });
                    break;

                case "saveRecommendations":
                    communityCommands.SaveRecommendationsLocal(root);
                    context.Send(new
                    {
                        type = "saved",
                        message = "本地内容已更新。"
                    });
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

    // 供上层适配记录无法处理的响应；写盘失败会被吞掉，不影响主流程。
    public void Log(string message) => context.Log(message);

    public void Dispose() => context.Dispose();
}
