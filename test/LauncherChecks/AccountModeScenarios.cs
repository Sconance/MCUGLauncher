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
        var messages = new List<JsonElement>();
        using var context = new LauncherContext(value => messages.Add(JsonSerializer.SerializeToElement(value)), store);
        var restores = 0;
        var modes = new AccountModeCommands(context, kind =>
        {
            restores++;
            return Task.FromResult(new MSession { Username = kind == "mua" ? "MuaName" : "MicrosoftName" });
        });
        foreach (var kind in new[] { "microsoft", "mua" })
        {
            await modes.SwitchAsync(kind);
            Check(context.Config.AccountType == kind && context.OnlineSession != null, "saved " + kind + " restores without interactive login");
            await modes.SwitchAsync("offline");
            Check(context.Config.Player == "OfflineName" && context.OnlineSession == null, "offline ID survives " + kind + " selection");
            await modes.SwitchAsync(kind);
            Check(context.Config.Player != "OfflineName", "switch back restores " + kind + " identity");
            await modes.SwitchAsync("offline");
        }
        Check(restores == 2, "repeat switches reuse verified sessions without network calls");
        Check(!messages.Any(m => m.GetProperty("type").GetString() == "state"), "switch sends only account state without rescanning instances");
        var now = DateTimeOffset.UtcNow;
        var cache = new AccountSessionCache(() => now);
        cache.Remember("mua", new MSession { Username = "CachedName" });
        now = now.AddMinutes(6);
        Check(cache.Find("mua") != null && cache.Fresh("mua") == null, "stale session remains selectable but requires verification before launch");
        context.AccountSessions.Forget("mua");
        var pending = new TaskCompletionSource<MSession>();
        var delayed = new AccountModeCommands(context, _ => pending.Task);
        var switchTask = delayed.SwitchAsync("mua");
        await modes.SwitchAsync("offline");
        pending.SetResult(new MSession { Username = "LateName" });
        await switchTask;
        Check(context.Config.AccountType == "offline" && context.Config.Player == "OfflineName", "late restoration cannot overwrite a newer selection");
        context.AccountSessions.Forget("microsoft");
        var networkFailure = await new AccountModeCommands(context, _ => throw new HttpRequestException("private payload"))
            .SwitchAsync("microsoft");
        Check(!networkFailure.NeedsLogin, "network failure does not request reauthentication");
        var expired = await new AccountModeCommands(context, _ => throw new AccountLoginException("expired", true))
            .SwitchAsync("mua");
        Check(expired.NeedsLogin, "expired MUA authorization requests login");
        Check(store.Load().OfflinePlayer == "OfflineName", "offline player persists independently");
    }

    static void Check(bool condition, string name)
    {
        if (!condition)
            throw new Exception(name);
        Console.WriteLine("PASS " + name);
    }
}
