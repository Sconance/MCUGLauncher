using System.Diagnostics;
using BlockLaunch;
using CmlLib.Core.Auth;
using MCUGLauncher.Services;
using static MCUGLauncher.UseCases.ProtocolValues;

namespace MCUGLauncher.UseCases;

internal sealed class GameCommands(LauncherContext context,
    Func<LaunchRequest, CancellationToken, Task<Process>>? buildProcess = null)
{
    bool preparing;

    public async Task LaunchAsync(string version)
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
            var key = string.IsNullOrWhiteSpace(version) ? context.Config.LastVersion : version;
            if (string.IsNullOrWhiteSpace(key))
                throw new IOException("还没有可启动的版本。");
            var player = context.Config.Player;
            var mode = context.Config.AccountType;
            var online = context.OnlineSession;
            if (mode != "offline" && online == null)
                throw new IOException("在线账号尚未验证完成，请完成登录后再启动。");
            var instance = InstanceSnapshot.Capture(context.Config, key);
            context.Tasks.RequireLaunchAvailable(instance.Root, key, instance.Directory);
            context.Games.RequireAvailable(instance.Directory, player, mode == "offline");
            context.MarkPreparing(instance.Root, instance.Directory);
            reservedDirectory = instance.Directory;
            var session = mode == "offline" ? new MSession
            {
                Username = player,
                UUID = OfflineUuid(player),
                AccessToken = "0",
                ClientToken = Guid.NewGuid().ToString("N"),
                UserType = "legacy"
            } : online!;
            AuthlibLaunch? external = null;
            if (mode == "mua" && context.Mua != null)
            {
                var metadata = await context.Mua.Metadata(context.Token);
                var jar = await AuthlibLaunch.Prepare(Path.Combine(context.Store.DataDirectory, "authlib-injector"), context.Http, context.Token);
                external = new AuthlibLaunch(jar, metadata.Json);
            }
            if (mode != "offline")
            {
                session = await new AccountModeCommands(context).EnsureFreshAsync(mode);
                if (!string.Equals(session.Username, player, StringComparison.Ordinal))
                    throw new IOException("账号身份已改变，请重新选择账号后启动。");
            }
            var settings = instance.Settings;
            settings.VersionInfo = "MCUGLauncher";
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
