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
        }, kind => Task.FromResult<AccountIdentity?>(new(kind == "mua" ? "MuaName" : "MicrosoftName", Guid.NewGuid().ToString("N"))));
        context.Config.AccountType = "microsoft";
        context.Config.Player = "MicrosoftName";
        await modes.RestoreSelectedAsync();
        Check(restores == 0 && context.Config.AccountType == "microsoft" && context.OnlineSession == null,
            "startup restores local selection without network authentication");
        await modes.SwitchAsync("offline");
        foreach (var kind in new[] { "microsoft", "mua" })
        {
            await modes.SwitchAsync(kind);
            Check(context.Config.AccountType == kind && context.OnlineSession == null && context.AccountSessions.Fresh(kind) == null, "saved " + kind + " selects locally without network or verified-session fabrication");
            await modes.SwitchAsync("offline");
            Check(context.Config.Player == "OfflineName" && context.OnlineSession == null, "offline ID survives " + kind + " selection");
            await modes.SwitchAsync(kind);
            Check(context.Config.Player != "OfflineName", "switch back restores " + kind + " identity");
            await modes.SwitchAsync("offline");
        }
        Check(restores == 0, "cold and repeated saved-account selections never invoke network authentication");
        foreach (var kind in new[] { "microsoft", "mua" })
        {
            await modes.EnsureFreshAsync(kind);
            Check(context.AccountSessions.Fresh(kind) != null, "authorization is performed on demand before launch: " + kind);
        }
        Check(restores == 2, "only explicit authorization requests restore the two sessions");
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
        Check(context.Config.AccountType == "offline" && context.Config.Player == "OfflineName", "late local identity read cannot overwrite a newer selection");
        messages.Clear();
        context.AccountSessions.Forget("microsoft");
        var localOnly = new AccountModeCommands(context, _ => throw new HttpRequestException("private payload"),
            _ => Task.FromResult<AccountIdentity?>(new("MicrosoftName", Guid.NewGuid().ToString("N"))));
        await localOnly.SwitchAsync("microsoft");
        Check(context.Config.AccountType == "microsoft" && context.OnlineSession == null, "saved identity remains selectable when network authentication is unavailable");
        try
        {
            await localOnly.EnsureFreshAsync("microsoft");
            throw new Exception("Network failure ignored");
        }
        catch (HttpRequestException) { Check(context.AccountSessions.Identity("microsoft") != null, "failed launch-time refresh preserves the selected local identity"); }
        context.AccountSessions.Forget("mua");
        var expired = new AccountModeCommands(context, _ => throw new AccountLoginException("expired", true),
            _ => Task.FromResult<AccountIdentity?>(new("MuaName", Guid.NewGuid().ToString("N"))));
        await expired.SwitchAsync("mua");
        try
        {
            await expired.EnsureFreshAsync("mua");
            throw new Exception("Expired session accepted");
        }
        catch (AccountLoginException) { Check(context.OnlineSession == null && context.AccountSessions.Fresh("mua") == null, "expired authorization cannot become a launch-ready session"); }
        messages.Clear();
        await new AccountModeCommands(context, readIdentity: _ => Task.FromResult<AccountIdentity?>(null)).SwitchAsync("mua");
        Check(messages.Last().GetProperty("needsLogin").GetBoolean(), "missing local identity requests login without attempting network restore");
        Check(store.Load().OfflinePlayer == "OfflineName", "offline player persists independently");
        var launchRoot = Path.Combine(directory, "local-selection-game");
        var versionDirectory = Path.Combine(launchRoot, "versions", "1.21.1");
        Directory.CreateDirectory(versionDirectory);
        File.WriteAllText(Path.Combine(versionDirectory, "1.21.1.json"), "{\"id\":\"1.21.1\",\"libraries\":[]}");
        var launchStore = new ConfigStore(Path.Combine(directory, "launch-guard"));
        launchStore.Save(new AppConfig { Root = launchRoot, Player = "SelectedOnly", AccountType = "microsoft", LastVersion = "1.21.1", LastInstance = "1.21.1" });
        var launchMessages = new List<JsonElement>();
        using (var launchContext = new LauncherContext(value => launchMessages.Add(FixtureMessages.Capture(value)), launchStore))
        {
            launchContext.AccountSessions.RememberIdentity("microsoft", new("SelectedOnly", Guid.NewGuid().ToString("N")));
            var builds = 0;
            await new GameCommands(launchContext, (_, _) => { builds++; throw new Exception("Unverified identity reached process builder"); }).LaunchAsync("1.21.1");
            Check(builds == 0 && launchMessages.Last().GetProperty("type").GetString() == "launchResult" && !launchMessages.Last().GetProperty("ok").GetBoolean(),
                "a local selected identity without authorization cannot reach game process creation");
        }
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
