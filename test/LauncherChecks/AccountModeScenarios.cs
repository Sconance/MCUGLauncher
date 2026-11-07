using System.IO;
using System.Net.Http;
using System.Text.Json;
using BlockLaunch;
using CmlLib.Core.Auth;
using MCUGLauncher.Models;
using MCUGLauncher.Services;
using MCUGLauncher.UseCases;

internal static class AccountModeScenarios
{
    public static async Task Run(string fixture)
    {
        var directory = Path.Combine(fixture, "account-modes");
        var store = new ConfigStore(directory);
        store.Save(new AppConfig { Player = "OfflineName", OfflinePlayer = "OfflineName", Root = fixture });
        var asyncStore = new ConfigStore(Path.Combine(fixture, "ordered-config-writes"));
        await Task.WhenAll(
            asyncStore.SaveAsync(new AppConfig { Player = "Older", Root = fixture }),
            asyncStore.SaveAsync(new AppConfig { Player = "Newer", Root = fixture }));
        Check(asyncStore.Load().Player == "Newer", "ordered async config writes cannot overwrite a newer snapshot");
        var messages = new List<JsonElement>();
        using var context = new LauncherContext(value => messages.Add(JsonSerializer.SerializeToElement(value)), store);
        var restores = 0;
        Task<AccountIdentity?> ReadIdentity(string kind) => Task.FromResult<AccountIdentity?>(
            new(kind == "mua" ? "MuaName" : "MicrosoftName", Guid.NewGuid().ToString("N")));
        var modes = new AccountModeCommands(context, kind =>
        {
            restores++;
            return Task.FromResult(new MSession { Username = kind == "mua" ? "MuaName" : "MicrosoftName" });
        }, ReadIdentity);
        context.Config.AccountType = "microsoft";
        context.Config.Player = "MicrosoftName";
        await modes.RestoreSelectedAsync();
        Check(restores == 0 && context.Config.AccountType == "microsoft" && context.OnlineSession == null,
            "startup restores local account selection without network authentication");
        await modes.SwitchAsync("offline");
        foreach (var kind in new[] { "microsoft", "mua" })
        {
            await modes.SwitchAsync(kind);
            Check(context.Config.AccountType == kind && context.OnlineSession == null && context.AccountSessions.Fresh(kind) == null,
                "saved " + kind + " selects locally without fabricating a verified session");
            await modes.SwitchAsync("offline");
            Check(context.Config.Player == "OfflineName" && context.OnlineSession == null, "offline ID survives " + kind + " selection");
            await modes.SwitchAsync(kind);
            Check(context.Config.Player != "OfflineName", "switch back restores " + kind + " identity");
            await modes.SwitchAsync("offline");
        }
        Check(restores == 0, "saved-account selection does not invoke network authentication");
        foreach (var kind in new[] { "microsoft", "mua" })
        {
            await modes.EnsureFreshAsync(kind);
            Check(context.AccountSessions.Fresh(kind) != null, "authorization is verified on demand: " + kind);
        }
        Check(restores == 2, "explicit authorization verifies each saved account");
        Check(!messages.Any(m => m.GetProperty("type").GetString() == "state"), "switch sends only account state without rescanning instances");
        var now = DateTimeOffset.UtcNow;
        var cache = new AccountSessionCache(() => now);
        cache.Remember("mua", new MSession { Username = "CachedName" });
        now = now.AddMinutes(6);
        Check(cache.Find("mua") != null && cache.Fresh("mua") == null, "stale session remains selectable but requires verification before launch");
        context.AccountSessions.Forget("mua");
        var pending = new TaskCompletionSource<AccountIdentity?>();
        var delayed = new AccountModeCommands(context, readIdentity: _ => pending.Task);
        var switchTask = delayed.SwitchAsync("mua");
        await modes.SwitchAsync("offline");
        pending.SetResult(new AccountIdentity("LateName", Guid.NewGuid().ToString("N")));
        await switchTask;
        Check(context.Config.AccountType == "offline" && context.Config.Player == "OfflineName", "late identity read cannot overwrite a newer selection");
        context.AccountSessions.Forget("microsoft");
        context.AccountSessions.RememberIdentity("microsoft", new("MicrosoftName", Guid.NewGuid().ToString("N")));
        try
        {
            await new AccountModeCommands(context, _ => throw new HttpRequestException("private payload"))
                .EnsureFreshAsync("microsoft");
            Check(false, "network failure is reported during authorization");
        }
        catch (HttpRequestException)
        {
            Check(true, "network failure does not masquerade as an account-selection failure");
        }
        context.AccountSessions.Forget("mua");
        var expired = await new AccountModeCommands(
            context,
            _ => throw new AccountLoginException("expired", true),
            _ => Task.FromResult<AccountIdentity?>(null))
            .SwitchAsync("mua");
        Check(expired.NeedsLogin, "missing local identity requests login");
        Check(store.Load().OfflinePlayer == "OfflineName", "offline player persists independently");
    }

    static void Check(bool condition, string name)
    {
        if (!condition)
            throw new Exception(name);
        Console.WriteLine("PASS " + name);
    }
}
