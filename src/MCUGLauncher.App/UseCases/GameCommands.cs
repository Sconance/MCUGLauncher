using System.Diagnostics;
using MCUGLauncher;
using CmlLib.Core.Auth;
using MCUGLauncher.Services;
using MCUGLauncher.Contracts;
using static MCUGLauncher.UseCases.ProtocolValues;

namespace MCUGLauncher.UseCases;

/// <summary>准备并启动独立游戏会话，观察退出并处理关闭请求。</summary>
/// <remarks>
/// B 类。成员级依赖与跨模块契约见 docs/components/GameCommands.md。
/// LauncherContext.Config/Mua/Token/Http/Store/Tasks/Games 提供启动快照与运行环境；
/// MarkPreparing(string,string)/ReleasePreparing(string) 保留准备状态；Send(object)/SendGames()/Log(string) 回推结果。
/// 不依赖任何 View、窗口或控件；语言设置复用 Core GameDefaults.EnsureChinese。
/// ServerLaunchInfo.InstanceId/ServerAddress 提供显式直连目标；Core ServerLaunch.Prepare 校验并解析直连参数。
/// </remarks>
internal sealed class GameCommands(LauncherContext context,
    Func<LaunchRequest, CancellationToken, Task<Process>>? buildProcess = null)
{
    bool preparing;

    public Task LaunchAsync(string version) => LaunchCoreAsync(version, null);

    public Task LaunchServerAsync(ServerLaunchInfo request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return LaunchCoreAsync(request.InstanceId, request.ServerAddress ?? "");
    }

    async Task LaunchCoreAsync(string version, string? serverAddress)
    {
        if (preparing)
        {
            Result(false, "游戏正在准备启动，请稍候。");
            return;
        }
        preparing = true;
        string? reservedDirectory = null;
        try
        {
            if (serverAddress != null)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(version);
                ArgumentException.ThrowIfNullOrWhiteSpace(serverAddress);
                serverAddress = ServerEndpoint.Parse(serverAddress).ToString();
            }
            var key = string.IsNullOrWhiteSpace(version) ? context.Config.LastVersion : version;
            if (string.IsNullOrWhiteSpace(key))
                throw new IOException("还没有可启动的版本。");
            var player = context.Config.Player;
            var mode = context.Config.AccountType;
            var instance = InstanceSnapshot.Capture(context.Config, key);
            if (serverAddress != null && instance.Settings.Isolation == "共享根目录")
                instance = instance with
                {
                    Directory = instance.Root
                };
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
            var settings = instance.Settings;
            if (settings.AutoChinese && !context.Games.UsesDirectory(instance.Directory))
                GameDefaults.EnsureChinese(instance.Directory, instance.Requirements.Minecraft, overwrite: true);
            settings.VersionInfo = "MCUGLauncher";
            if (settings.AutoMemory)
            {
                settings.RamMb = MemoryPolicy.Recommend();
                if (File.Exists(settings.Java) && JavaDiscovery.Architecture(settings.Java) == "x86")
                    settings.RamMb = Math.Min(settings.RamMb, 1400);
            }
            var request = new LaunchRequest(instance.Root, instance.Directory, key, settings.Java, settings.RamMb,
                settings.Width, settings.Height, session, settings, external);
            if (serverAddress != null)
            {
                var direct = ServerLaunch.Prepare(new ServerLaunchRequest(instance.Root, key, serverAddress, session,
                    Path.Combine(context.Store.DataDirectory, "game-logs", "server-launch.log"), settings, external), context.Token);
                if (!Path.GetFullPath(direct.InstanceDirectory).Equals(Path.GetFullPath(instance.Directory), StringComparison.OrdinalIgnoreCase))
                    throw new IOException("实例目录已改变，请重新启动。");
                request = direct with
                {
                    Ram = settings.RamMb
                };
            }
            var process = buildProcess == null ? await LaunchService.Build(request, context.Token, context.Http)
                : await buildProcess(request, context.Token);
            context.Token.ThrowIfCancellationRequested();
            var game = context.Games.Start(instance.Root, instance.Directory, key,
                Path.Combine(context.Store.DataDirectory, "game-logs"), process, context.Log,
                new[] { session.AccessToken ?? "" }, player, mode == "offline");
            context.SendGames();
            _ = ObserveAsync(game);
            if (Path.GetFullPath(context.Config.Root).Equals(instance.Root, StringComparison.OrdinalIgnoreCase))
            {
                context.Config.LastVersion = key;
                context.Config.LastInstance = key;
                context.Store.Save(context.Config);
            }
            Result(true, "已启动 " + key);
        }
        catch (Exception ex) { Result(false, ex.Message); }
        finally
        {
            if (reservedDirectory != null)
                context.ReleasePreparing(reservedDirectory);
            preparing = false;
        }
    }

    void Result(bool ok, string message) => context.Send(new { type = "launchResult", ok, message });

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
