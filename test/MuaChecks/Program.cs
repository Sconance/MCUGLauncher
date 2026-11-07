using BlockLaunch;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

var root = Path.GetFullPath("work/v0170-mua-checks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var one = new MuaProfile("0123456789abcdef0123456789abcdef", "MuaTester");
var two = new MuaProfile("1123456789abcdef0123456789abcdef", "OtherTester");
void Check(bool value, string name)
{
    if (!value)
        throw new Exception(name);
    Console.WriteLine("PASS " + name);
}

async Task Fails(Func<Task> action, string name)
{
    try
    {
        await action();
        throw new Exception("Expected failure: " + name);
    }
    catch (Exception ex) when (ex is IOException or AccountLoginException or OperationCanceledException)
    {
        Check(!ex.Message.Contains("test-private-password") && !ex.Message.Contains("token-private-"), name);
    }
}

HttpResponseMessage Json(object value, HttpStatusCode status = HttpStatusCode.OK) => new(status)
{
    Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json")
};
var handler = new Mock();
using var http = new HttpClient(handler);
using var service = new MuaAccountService(root, http);
string currentClient = "";
HttpResponseMessage Auth(JsonElement body, object? selected, object[]? profiles = null)
{
    currentClient = body.GetProperty("clientToken").GetString()!;
    return Json(new
    {
        accessToken = "token-private-original",
        clientToken = currentClient,
        selectedProfile = selected,
        availableProfiles = profiles ?? []
    });
}

handler.Respond = (r, b) => Auth(b!.Value, new { id = one.Id, name = one.Name });
var saved = await service.Login("test-account", "test-private-password", _ => throw new Exception("Single bound profile must not prompt"), default);
Check(saved.Profile == one && handler.Paths.Single().EndsWith("/authserver/authenticate"), "bound profile logs in using fixed MUA API");
Check(handler.Bodies[0].GetProperty("requestUser").GetBoolean() && handler.Bodies[0].GetProperty("agent").GetProperty("name").GetString() == "Minecraft", "authenticate sends Minecraft agent and requests user info");
var encrypted = await File.ReadAllBytesAsync(Path.Combine(root, "mua-account.bin"));
Check(!Encoding.UTF8.GetString(encrypted).Contains("token-private") && !Encoding.UTF8.GetString(encrypted).Contains("test-private-password"), "cache is DPAPI encrypted and contains no plaintext password or token");
Check(service.Read()!.Profile == one, "encrypted saved role round-trips");
var session = MuaAccountService.Session(saved);
Check(session.UserType == "mojang" && session.UUID == one.Id && session.Username == one.Name && session.AccessToken == saved.AccessToken, "MUA session keeps server UUID and mojang user type");
handler.Respond = (r, b) => new(HttpStatusCode.NoContent);
var before = handler.Paths.Count;
await service.Ensure(false, default);
Check(handler.Paths.Count == before + 1 && handler.Paths.Last().EndsWith("/validate"), "valid token uses validate without unnecessary refresh");
handler.Respond = (r, b) => r.RequestUri!.AbsolutePath.EndsWith("validate") ? new(HttpStatusCode.Forbidden) : Json(new { accessToken = "token-private-refreshed", clientToken = currentClient, selectedProfile = new { id = one.Id, name = "RenamedTester" } });
var refreshed = await service.Ensure(false, default);
Check(refreshed.Profile.Name == "RenamedTester" && service.Read()!.AccessToken == "token-private-refreshed", "expired token refresh rotates cache and updates role name");
Check(!handler.Bodies.Last().TryGetProperty("selectedProfile", out _), "bound refresh does not attempt to rebind profile");
handler.Respond = (r, b) => Json(new { accessToken = "token-private-wrong-role", clientToken = currentClient, selectedProfile = new { id = two.Id, name = two.Name } });
await Fails(() => service.Ensure(true, default), "refresh refuses a changed UUID");
Check(service.Read()!.Profile.Id == one.Id, "failed refresh preserves saved identity");
handler.Respond = (r, b) => r.RequestUri!.AbsolutePath.EndsWith("authenticate") ? Auth(b!.Value, null, [new { id = one.Id, name = one.Name }, new { id = two.Id, name = two.Name }]) : Json(new { accessToken = "token-private-selected", clientToken = currentClient, selectedProfile = new { id = two.Id, name = two.Name } });
var selected = await service.Login("test-account", "test-private-password", list => Task.FromResult<MuaProfile?>(list[1]), default);
Check(selected.Profile == two, "multiple unbound profiles select and bind requested role");
Check(handler.Bodies.Last().GetProperty("selectedProfile").GetProperty("id").GetString() == two.Id, "selected role is sent through refresh");
handler.Respond = (r, b) => Auth(b!.Value, null, [new { id = one.Id, name = one.Name }, new { id = two.Id, name = two.Name }]);
await Fails(() => service.Login("test-account", "test-private-password", _ => Task.FromResult<MuaProfile?>(null), default), "cancelled role choice does not save partial login");
Check(service.Read()!.Profile == two, "cancel preserves previous account");
handler.Respond = (r, b) => Auth(b!.Value, null);
await Fails(() => service.Login("test-account", "test-private-password", _ => Task.FromResult<MuaProfile?>(null), default), "no-role account gets actionable failure");
handler.Respond = (r, b) => new(HttpStatusCode.TemporaryRedirect)
{
    Headers =
    {
        Location = new Uri("https://example.com/steal")
    }
};
await Fails(() => service.Login("test-account", "test-private-password", _ => Task.FromResult<MuaProfile?>(null), default), "credential redirect is refused");
handler.Respond = (r, b) => Json(new { errorMessage = "test-private-password token-private-echo" }, HttpStatusCode.Forbidden);
await Fails(() => service.Ensure(false, default), "invalid token requires login and never echoes error body secrets");
handler.Respond = (r, b) => new(HttpStatusCode.ServiceUnavailable);
Check(!await service.Forget(default) && service.Read() == null, "offline logout clears local credential and reports unconfirmed revocation");
handler.Respond = (r, b) => Auth(b!.Value, new { id = one.Id, name = one.Name });
await service.Login("test-account", "test-private-password", _ => Task.FromResult<MuaProfile?>(null), default);
handler.Respond = (r, b) => new(HttpStatusCode.NoContent);
Check(await service.Forget(default) && handler.Paths.Last().EndsWith("/invalidate"), "logout invalidates token and clears cache");
var metadata = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "mua-metadata.json"));
if (File.Exists("work/verify/authlib-injector-1.2.8.jar"))
{
    var jarDirectory = Path.Combine(root, "中文 agent path");
    Directory.CreateDirectory(jarDirectory);
    var jar = Path.Combine(jarDirectory, "authlib-injector-1.2.8.jar");
    File.Copy("work/verify/authlib-injector-1.2.8.jar", jar);
    var external = new AuthlibLaunch(jar, metadata);
    var arguments = external.Arguments();
    Check(arguments[0].Contains("中文 agent path") && arguments[0].EndsWith("=" + MuaAccountService.Api) && arguments.Any(a => a.StartsWith("-Dauthlibinjector.yggdrasil.prefetched=")), "agent arguments preserve spaces and prefetch metadata");
    File.WriteAllText(jar, "broken");
    Check(!AuthlibLaunch.ValidJar(jar), "corrupt agent is rejected");
    try
    {
        external.Arguments();
        throw new Exception("corrupt agent accepted");
    }
    catch (IOException)
    {
        Console.WriteLine("PASS corrupt agent cannot build launch arguments");
    }

    var jarBytes = await File.ReadAllBytesAsync("work/verify/authlib-injector-1.2.8.jar");
    using var download = new HttpClient(new Mock { Respond = (_, _) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(jarBytes) } });
    await AuthlibLaunch.Prepare(jarDirectory, download, default);
    Check(AuthlibLaunch.ValidJar(jar), "agent cache is repaired only with matching SHA-256");
}
else
    Console.WriteLine("SKIP pinned-agent fixture checks; use IntegrationChecks --mua-agent to download and verify the real agent");
handler.Respond = (r, b) => Json(new { id = one.Id, properties = new[] { new { name = "textures", value = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { textures = new { SKIN = new { url = "https://skin.mualliance.ltd/textures/test.png" } } }))) } } });
var cred = new MuaCredential
{
    Account = "fixture",
    AccessToken = "token",
    ClientToken = "client",
    Profile = one
};
Check((await service.Skin(cred, new(metadata, ["skin.mualliance.ltd"]), default))!.Host == "skin.mualliance.ltd", "MUA skin uses matching profile and declared HTTPS domain");
await Fails(async () =>
{
    await service.Skin(cred, new(metadata, ["unrelated.example"]), default);
}, "MUA skin rejects undeclared origin");
Check(MuaAccountService.IsMuaConfiguration("authlib-injector:yggdrasil-server:" + Uri.EscapeDataString(MuaAccountService.Api)), "MUA website drag configuration is recognized");
Check(!MuaAccountService.IsMuaConfiguration("https://skin.mualliance.ltd.evil.example/api/yggdrasil/") && !MuaAccountService.IsMuaConfiguration("http://skin.mualliance.ltd/api/yggdrasil/"), "MUA drag rejects lookalike and plaintext destinations");
handler.Respond = (_, _) => throw new TaskCanceledException("mock timeout");
try
{
    await service.Login("fixture", "test-private-password", _ => Task.FromResult<MuaProfile?>(null), default);
    throw new Exception("Timeout not detected");
}
catch (IOException ex)
{
    Check(ex.Message.Contains("超时"), "transport timeout is an actionable error, not user cancellation");
}

new ProtectedAccountStorage(Path.Combine(root, "mua-account.bin")).Write(System.Text.Json.Nodes.JsonNode.Parse("{\"AccessToken\":[]}")!, null);
try
{
    service.Read();
    throw new Exception("Malformed cache accepted");
}
catch (AccountStorageException)
{
    Console.WriteLine("PASS malformed encrypted cache is contained and preserved");
}

Console.WriteLine("ALL_MUA_CHECKS_PASSED (mock authentication; no human credentials used)");
sealed class Mock : HttpMessageHandler
{
    public Func<HttpRequestMessage, JsonElement?, HttpResponseMessage> Respond = (_, _) => new(HttpStatusCode.NoContent);
    public List<string> Paths = [];
    public List<JsonElement> Bodies = [];

    /// <summary>
    /// 发送。
    /// </summary>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        if (request.RequestUri!.Host != "skin.mualliance.ltd" && request.RequestUri.Host != "authlib-injector.yushi.moe")
            throw new Exception("Unexpected destination");
        Paths.Add(request.RequestUri.AbsolutePath);
        JsonElement? body = null;
        if (request.Content != null)
        {
            using var json = JsonDocument.Parse(await request.Content.ReadAsStringAsync(token));
            body = json.RootElement.Clone();
            Bodies.Add(body.Value);
        }

        return Respond(request, body);
    }
}
