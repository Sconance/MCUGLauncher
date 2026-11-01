using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BlockLaunch;
using CmlLib.Core.Auth;
using CmlLib.Core.Auth.Microsoft.Sessions;
using Microsoft.Extensions.Logging.Abstractions;
using XboxAuthNet.Game.Accounts;
using XboxAuthNet.Game.Authenticators;
using XboxAuthNet.Game.SessionStorages;

// An already-cancelled login must not create a native window or require an Application.
using (var cancelledLogin = new CancellationTokenSource())
{
    cancelledLogin.Cancel();
    try
    {
        await new MicrosoftLoginWebUi(Path.GetTempPath()).DisplayDialogAndInterceptUri(
            new Uri("https://login.live.com/"), null!, cancelledLogin.Token);
        throw new Exception("Cancelled login opened a UI");
    }
    catch (OperationCanceledException) { Console.WriteLine("PASS cancelled login does not open a browser window"); }
}

if (args.Contains("--avatar") || args.Contains("--avatar-live"))
{
    AvatarChecks.Run(args.Contains("--avatar-live"));
    return;
}

if (args.Contains("--status") || args.Contains("--live"))
{
    using var liveHttp = new HttpClient();
    liveHttp.DefaultRequestHeaders.UserAgent.ParseAdd("MCUGLauncher (account-verification)");
    var liveService = new MicrosoftAccountService(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BlockLaunch", "accounts"), liveHttp);
    Console.WriteLine("SAVED_MICROSOFT_ACCOUNT=" + (liveService.ReadSavedAccount() != null));
    if (args.Contains("--live"))
    {
        var session = await liveService.Authenticate(false, CancellationToken.None);
        MicrosoftAccountService.ValidateSession(session);
        Console.WriteLine("LIVE_REFRESH_PROFILE_AND_OWNERSHIP_PASSED");
        if (args.Contains("--game"))
        {
            var gameRoot = Path.GetFullPath("work/test-minecraft");
            var catalog = new InstanceCatalog(gameRoot);
            var key = catalog.Create("Microsoft online verification", "1.21.1", "1.21.1");
            var directory = catalog.DirectoryFor(key);
            var process = await LaunchService.Build(new(gameRoot, directory, "1.21.1", "", 2048, 854, 480, session), CancellationToken.None);
            var gameSession = new GameSession(process, Path.GetFullPath("work/v051-online-game.log"), _ =>
            {
            }, [session.AccessToken ?? ""]);
            var started = DateTime.UtcNow;
            gameSession.Start();
            try
            {
                var rendered = false;
                var log = Path.Combine(directory, "logs", "latest.log");
                for (var i = 0; i < 60; i++)
                {
                    await Task.Delay(2000);
                    if (process.HasExited)
                        throw new Exception("Online game exited with code " + process.ExitCode);
                    if (!File.Exists(log) || File.GetLastWriteTimeUtc(log) < started)
                        continue;
                    var tail = SharedLogs.ReadTail(log);
                    if (tail.Contains("OpenGL") || tail.Contains("Backend library: LWJGL"))
                    {
                        rendered = true;
                        break;
                    }
                }

                if (!rendered)
                    throw new Exception("Online game renderer was not observed");
                await Task.Delay(8000);
                if (process.HasExited)
                    throw new Exception("Online game exited during renderer validation");
                Console.WriteLine("LIVE_MICROSOFT_SESSION_GAME_RENDER_PASSED");
                gameSession.RequestStop();
                try
                {
                    var exit = await gameSession.Completion.WaitAsync(TimeSpan.FromSeconds(15));
                    if (exit.Code != 0)
                        throw new Exception("Unexpected game exit " + exit.Code);
                    Console.WriteLine("LIVE_MICROSOFT_GAME_NORMAL_EXIT_PASSED");
                }
                catch (TimeoutException)
                {
                    process.Kill(true);
                    await gameSession.Completion;
                    throw new Exception("Online game did not close within the test timeout");
                }
            }
            finally
            {
                if (!process.HasExited)
                {
                    process.Kill(true);
                    await gameSession.Completion;
                }
            }
        }
    }

    return;
}

var root = Path.GetFullPath("work/auth-check-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
void Check(bool pass, string label)
{
    if (!pass)
        throw new Exception(label);
    Console.WriteLine(label + " OK");
}

var path = Path.Combine(root, "microsoft-account.dat");
var store = new ProtectedAccountStorage(path);
store.Write(JsonNode.Parse("{\"access_token\":\"fake-access-token-for-test\",\"refresh_token\":\"fake-refresh-token-for-test\"}")!, null);
Check(!Encoding.UTF8.GetString(File.ReadAllBytes(path)).Contains("fake-access-token-for-test"), "DPAPI_FILE_HAS_NO_PLAINTEXT_TOKEN");
Check(store.ReadAsJsonNode()?["refresh_token"]?.GetValue<string>() == "fake-refresh-token-for-test", "DPAPI_ROUNDTRIP_CURRENT_USER");
File.WriteAllBytes(path, [1, 2, 3]);
try
{
    store.ReadAsJsonNode();
    throw new Exception("Corrupt account accepted");
}
catch (AccountStorageException)
{
}

Check(File.ReadAllBytes(path).SequenceEqual(new byte[] { 1, 2, 3 }), "CORRUPT_CACHE_IS_NOT_SILENTLY_OVERWRITTEN");
store.Forget();
var manager = new JsonXboxGameAccountManager(store, JEGameAccount.FromSessionStorage, JsonXboxGameAccountManager.DefaultSerializerOption);
_ = manager.GetAccounts();
var account = manager.NewAccount();
JEProfileSource.Default.Set(account.SessionStorage, new JEProfile { Username = "AuthTester", UUID = "123456781234123412341234567890ab" });
JETokenSource.Default.Set(account.SessionStorage, new JEToken { AccessToken = "fake-game-token", ExpiresOn = DateTime.UtcNow.AddHours(1) });
manager.SaveAccounts();
using var unusedHttp = new HttpClient(new Handler(HttpStatusCode.OK, "{}"));
var service = new MicrosoftAccountService(root, unusedHttp);
Check(service.ReadSavedAccount()?.Username == "AuthTester", "LIBRARY_ACCOUNT_METADATA_RELOADS_FROM_ENCRYPTED_CACHE");
var before = File.ReadAllBytes(path);
try
{
    await service.Authenticate(false, CancellationToken.None);
    throw new Exception("Missing OAuth session accepted");
}
catch (AccountLoginException ex)
{
    Check(ex.RequiresInteractive, "MISSING_REFRESH_SESSION_REQUIRES_INTERACTIVE_LOGIN");
}

Check(File.ReadAllBytes(path).SequenceEqual(before), "FAILED_SILENT_LOGIN_PRESERVES_CACHE");
var sessions = new InMemorySessionStorage();
JETokenSource.Default.Set(sessions, new JEToken { AccessToken = "fake-game-token" });
async Task Guard(HttpStatusCode status, string body, bool allowed, bool requiresInteractive = false)
{
    var handler = new Handler(status, body);
    using var http = new HttpClient(handler);
    try
    {
        await new MinecraftOwnershipGuard().ExecuteAsync(new AuthenticateContext(sessions, http, CancellationToken.None, NullLogger.Instance));
        Check(allowed, "JAVA_OWNERSHIP_ACCEPTED");
    }
    catch (AccountLoginException ex)
    {
        Check(!allowed && ex.RequiresInteractive == requiresInteractive, "JAVA_OWNERSHIP_DENIED_" + (int)status);
    }

    Check(handler.AuthChecked, "OWNERSHIP_REQUEST_USES_BEARER_AND_OFFICIAL_HOST");
}

await Guard(HttpStatusCode.OK, "{\"items\":[{\"name\":\"game_minecraft\"}]}", true);
await Guard(HttpStatusCode.OK, "{\"items\":[]}", false);
await Guard(HttpStatusCode.OK, "{\"items\":[{\"name\":\"unrelated_product\"}]}", false);
await Guard(HttpStatusCode.Unauthorized, "{}", false, true);
await Guard(HttpStatusCode.TooManyRequests, "{}", false);
await Guard(HttpStatusCode.ServiceUnavailable, "{}", false);
using (var cancelled = new CancellationTokenSource())
using (var http = new HttpClient(new WaitingHandler()))
{
    var pending = new MinecraftOwnershipGuard().ExecuteAsync(new AuthenticateContext(sessions, http, cancelled.Token, NullLogger.Instance)).AsTask();
    cancelled.Cancel();
    try
    {
        await pending;
        throw new Exception("Cancellation ignored");
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("OWNERSHIP_CHECK_CANCELLABLE OK");
    }
}

string Segment(object obj) => Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(obj))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
var valid = new MSession
{
    Username = "AuthTester",
    UUID = "123456781234123412341234567890ab",
    UserType = "msa",
    AccessToken = Segment(new
    {
        alg = "test"
    }) + "." + Segment(new
    {
        exp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()
    }) + ".test-signature"
};
MicrosoftAccountService.ValidateSession(valid);
try
{
    MicrosoftAccountService.ValidateSession(MSession.CreateOfflineSession("Tester"));
    throw new Exception("Offline session accepted as online");
}
catch (AccountLoginException)
{
    Console.WriteLine("OFFLINE_SESSION_CANNOT_PASS_ONLINE_VALIDATION OK");
}

Check(!MicrosoftAccountService.Translate(new Exception("access_token=private-test-token"), false).Message.Contains("private-test-token"), "AUTH_ERRORS_DO_NOT_EXPOSE_PAYLOADS");
service.Forget();
Check(service.ReadSavedAccount() == null, "SIGNOUT_CLEARS_SAVED_ACCOUNT");
Console.WriteLine("ALL_AUTH_CHECKS_PASSED");
sealed class Handler(HttpStatusCode status, string body) : HttpMessageHandler
{
    public bool AuthChecked
    {
        get;
        private set;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        AuthChecked = request.RequestUri?.Host == "api.minecraftservices.com" && request.Headers.Authorization?.Scheme == "Bearer" && request.Headers.Authorization.Parameter == "fake-game-token";
        return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
    }
}

sealed class WaitingHandler : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        await Task.Delay(Timeout.Infinite, token);
        return new(HttpStatusCode.OK);
    }
}
