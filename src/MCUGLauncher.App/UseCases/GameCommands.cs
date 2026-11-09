using System.Diagnostics;
using BlockLaunch;
using CmlLib.Core.Auth;
using MCUGLauncher.Contracts;
using MCUGLauncher.Services;
using static MCUGLauncher.UseCases.ProtocolValues;

namespace MCUGLauncher.UseCases;

/// <summary>普通启动、指定服务器启动、会话停止与进程观察的统一用例。</summary>
/// <remarks>B 类：通过 LauncherContext.Config/Games/Tasks/Store/Http/Token/Log/SendGames
/// 和 MarkPreparing/ReleasePreparing 管理启动；依赖 InstanceSnapshot.Capture、
/// AccountModeCommands.EnsureFreshAsync 和 MuaAccountService.Metadata。
/// 详细成员和 Core 调用见 docs/components/GameCommands.md；不依赖视图或 LauncherService。</remarks>
internal sealed class GameCommands(LauncherContext context,
    Func<LaunchRequest, CancellationToken, Task<Process>>? buildProcess = null)
{
    bool preparing;

    public Task<LaunchResultInfo> LaunchAsync(string version) => LaunchCoreAsync(version, null);

    public Task<LaunchResultInfo> LaunchManagedAsync(InstanceTarget target, string? serverAddress)
    {
        if (string.IsNullOrWhiteSpace(target.Root) || string.IsNullOrWhiteSpace(target.Name))
            return Task.FromResult(new LaunchResultInfo(false, "必须明确指定游戏根目录和实例名称。"));
        return serverAddress is null ? LaunchCoreAsync(target.Name, null, target.Root)
            : LaunchToServerAsync(target.Name, serverAddress, target.Root);
    }

    string? FavoriteRoot(string instance, string address)
    {
        foreach (var server in context.Config.Servers.Where(server =>
            string.Equals(server.Instance, instance, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(server.GameRoot)))
        {
            try
            {
                if (ServerEndpoint.Parse(server.Address).ToString().Equals(address, StringComparison.OrdinalIgnoreCase))
                    return server.GameRoot;
            }
            catch (Exception error) when (error is IOException or ArgumentException) { }
        }
        return null;
    }

    public async Task<LaunchResultInfo> LaunchToServerAsync(string instance, string serverAddress, string? gameRoot = null)
    {
        if (string.IsNullOrWhiteSpace(instance))
            return new(false, "请先为服务器收藏绑定启动实例。");
        if (string.IsNullOrWhiteSpace(serverAddress))
            return new(false, "请填写服务器地址。");
        string address;
        try
        {
            address = ServerEndpoint.Parse(serverAddress).ToString();
        }
        catch (Exception error) when (error is IOException or ArgumentException)
        {
            return new(false, error.Message);
        }
        gameRoot ??= FavoriteRoot(instance.Trim(), address);
        return await LaunchCoreAsync(instance.Trim(), address, gameRoot);
    }

    async Task<LaunchResultInfo> LaunchCoreAsync(string version, string? serverOverride, string? gameRoot = null)
    {
        if (preparing)
            return new(false, "游戏正在准备启动，请稍候。");
        preparing = true;
        string? reservedDirectory = null;
        try
        {
            var key = string.IsNullOrWhiteSpace(version) ? context.Config.LastVersion : version;
            if (string.IsNullOrWhiteSpace(key))
                throw new IOException("还没有可启动的版本。");
            var player = context.Config.Player;
            var mode = context.Config.AccountType;
            var instance = InstanceSnapshot.Capture(context.Config, key, gameRoot);
            context.Tasks.RequireLaunchAvailable(instance.Root, key, instance.Directory);
            context.Games.RequireAvailable(instance.Directory, player, mode == "offline");
            context.MarkPreparing(instance.Root, instance.Directory);
            reservedDirectory = instance.Directory;
            MSession session;
            if (mode == "offline")
                session = new MSession
                {
                    Username = player,
                    UUID = OfflineUuid(player),
                    AccessToken = "0",
                    ClientToken = Guid.NewGuid().ToString("N"),
                    UserType = "legacy"
                };
            else
            {
                session = await new AccountModeCommands(context).EnsureFreshAsync(mode);
                if (!string.Equals(session.Username, player, StringComparison.Ordinal))
                    throw new IOException("账号身份已改变，请重新选择账号后启动。");
            }
            AuthlibLaunch? external = null;
            if (mode == "mua" && context.Mua != null)
            {
                var metadata = await context.Mua.Metadata(context.Token);
                var jar = await AuthlibLaunch.Prepare(Path.Combine(context.Store.DataDirectory, "authlib-injector"), context.Http, context.Token);
                external = new AuthlibLaunch(jar, metadata.Json);
            }
            // InstanceSnapshot owns a deep copy, so this override cannot alter saved settings.
            var settings = instance.Settings;
            if (serverOverride != null)
                settings.Server = serverOverride;
            if (settings.AutoChinese && !context.Games.UsesDirectory(instance.Directory))
                GameDefaults.EnsureChinese(instance.Directory, instance.Requirements.Minecraft);
            settings.VersionInfo = "MCUGLauncher";
            if (settings.AutoMemory)
            {
                settings.RamMb = MemoryPolicy.Recommend();
                if (File.Exists(settings.Java) && JavaDiscovery.Architecture(settings.Java) == "x86")
                    settings.RamMb = Math.Min(settings.RamMb, 1400);
            }
            var request = new LaunchRequest(instance.Root, instance.Directory, key, settings.Java, settings.RamMb,
                settings.Width, settings.Height, session, settings, external);
            var process = buildProcess == null ? await LaunchService.Build(request, context.Token, context.Http)
                : await buildProcess(request, context.Token);
            context.Token.ThrowIfCancellationRequested();
            var game = context.Games.Start(instance.Root, instance.Directory, key,
                Path.Combine(context.Store.DataDirectory, "game-logs"), process, context.Log,
                new[] { session.AccessToken ?? "" }, player, mode == "offline");
            context.SendGames();
            _ = ObserveAsync(game);
            if (GameDirectoryPath.Comparer.Equals(GameDirectoryPath.Normalize(context.Config.Root), instance.Root))
            {
                context.Config.LastVersion = key;
                context.Config.LastInstance = key;
                context.Store.Save(context.Config);
            }
            return new(true, "已启动 " + key);
        }
        catch (Exception ex) { return new(false, ex.Message); }
        finally
        {
            if (reservedDirectory != null)
                context.ReleasePreparing(reservedDirectory);
            preparing = false;
        }
    }

    async Task ObserveAsync(RunningGame game)
    {
        try
        {
            await game.Session.Completion;
        }
        catch (Exception ex) { context.Log(ex.ToString()); }
        finally { context.Games.Release(game.Id); context.SendGames(); }
    }

    public void Stop(string id)
    {
        var game = context.Games.Active.FirstOrDefault(g => g.Id.ToString("N") == id)
            ?? throw new InvalidOperationException("该游戏已经退出。");
        if (!game.Session.RequestStop())
            throw new IOException("关闭请求未成功，请稍后重试。");
    }
}
