using System.IO;
using System.Net.Http;
using System.Text.Json;
using MCUGLauncher;
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
        using var context = new LauncherContext(value => messages.Add(FixtureMessages.Capture(value)), store);
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
        messages.Clear();
        context.AccountSessions.Forget("microsoft");
        await new AccountModeCommands(context, _ => throw new HttpRequestException("private payload")).SwitchAsync("microsoft");
        Check(!messages.Last().GetProperty("needsLogin").GetBoolean(), "network failure does not request reauthentication");
        messages.Clear();
        await new AccountModeCommands(context, _ => throw new AccountLoginException("expired", true)).SwitchAsync("mua");
        Check(messages.Last().GetProperty("needsLogin").GetBoolean(), "expired MUA authorization requests login");
        Check(store.Load().OfflinePlayer == "OfflineName", "offline player persists independently");
        var pendingWrites = new List<Task>();
        for (var i = 0; i < 20; i++)
        {
            context.Config.Player = "Saved" + i;
            pendingWrites.Add(store.SaveAsync(context.Config));
        }
        context.Config.Player = "FinalSaved";
        store.Save(context.Config);
        await Task.WhenAll(pendingWrites);
        Check(store.Load().Player == "FinalSaved", "older asynchronous snapshots cannot overwrite a newer synchronous save");
        context.Config.Player = "OfflineName";
        context.Config.AccountType = "offline";
        var encoded = 0;
        var portraits = new AvatarCommands(context, bytes => "fixture-" + (++encoded));
        await portraits.ReadAsync(force: false);
        var firstImage = messages.Last().GetProperty("image").GetString();
        await portraits.ReadAsync(force: false);
        Check(encoded == 1 && messages.Last().GetProperty("image").GetString() == firstImage, "automatic repeated avatar reads reuse the rendered cache");
        await portraits.ReadAsync(force: true);
        Check(messages.Last().GetProperty("image").GetString() == firstImage, "manual fallback refresh preserves the reusable default portrait");
        var delayedReads = new AvatarCommands(context, _ => "coalesced-" + (++encoded));
        var initialEncodes = encoded;
        await Task.WhenAll(delayedReads.ReadAsync(false), delayedReads.ReadAsync(false));
        Check(encoded == initialEncodes + 1, "concurrent automatic skin reads share one render task");
        var beforeLate = messages.Count;
        await new AvatarCommands(context, _ =>
        {
            Interlocked.Increment(ref context.AccountRevision);
            return "late";
        }).ReadAsync(false);
        Check(messages.Count == beforeLate, "late skin load cannot publish over a newer account revision");
        var skin = Path.Combine(directory, "mock-skin.png");
        File.WriteAllBytes(skin, new byte[] { 1, 2, 3 });
        context.Config.SkinFile = skin;
        var localReads = new AvatarCommands(context, _ => "local-" + (++encoded));
        await localReads.ReadAsync(false);
        var beforeForce = messages.Last().GetProperty("image").GetString();
        await localReads.ReadAsync(true);
        Check(messages.Last().GetProperty("image").GetString() != beforeForce, "explicit skin refresh bypasses the automatic rendered cache");


    }

    static void Check(bool condition, string name)
    {
        if (!condition)
            throw new Exception(name);
        Console.WriteLine("PASS " + name);
    }
}
