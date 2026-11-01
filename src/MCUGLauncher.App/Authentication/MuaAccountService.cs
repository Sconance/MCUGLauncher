using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CmlLib.Core.Auth;

namespace BlockLaunch;

/// <summary>
/// 表示 Mua档案 数据。
/// </summary>
public record MuaProfile(string Id, string Name);

/// <summary>
/// 提供 Mua凭据 相关功能。
/// </summary>
public sealed class MuaCredential
{
    /// <summary>
    /// 获取或设置 服务器。
    /// </summary>
    public string Server { get; set; } = MuaAccountService.Api;
    /// <summary>
    /// 获取或设置 账号。
    /// </summary>
    public string Account { get; set; } = "";
    /// <summary>
    /// 获取或设置 Access令牌。
    /// </summary>
    public string AccessToken { get; set; } = "";
    /// <summary>
    /// 获取或设置 Client令牌。
    /// </summary>
    public string ClientToken { get; set; } = "";
    /// <summary>
    /// 获取或设置 档案。
    /// </summary>
    public MuaProfile Profile { get; set; } = new("", "");

    /// <summary>
    /// 执行 ToString。
    /// </summary>
    public override string ToString() => "MUA credential (protected)";
}

/// <summary>
/// 表示 MuaMetadata 数据。
/// </summary>
public sealed record MuaMetadata(string Json, IReadOnlyList<string> SkinDomains);

/// <summary>
/// 提供 Mua账号Service 相关功能。
/// </summary>
public sealed class MuaAccountService : IDisposable
{
    public const string Api = "https://skin.mualliance.ltd/api/yggdrasil/";

    /// <summary>
    /// 判断是否为MuaConfiguration。
    /// </summary>
    public static bool IsMuaConfiguration(string text)
    {
        if (text.Length > 2048)
            return false;
        const string prefix = "authlib-injector:yggdrasil-server:";
        try
        {
            if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                text = Uri.UnescapeDataString(text[prefix.Length..]);
            return Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.Host == "skin.mualliance.ltd" && uri.IsDefaultPort && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0 && uri.AbsolutePath.TrimEnd('/') == "/api/yggdrasil";
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    readonly ProtectedAccountStorage storage;
    readonly HttpClient http;
    readonly bool ownsHttp;
    readonly SemaphoreSlim gate = new(1);

    public MuaAccountService(string directory, HttpClient? client = null)
    {
        storage = new(Path.Combine(directory, "mua-account.bin"));
        ownsHttp = client == null;
        http = client ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromSeconds(25)
        };
        if (http.DefaultRequestHeaders.UserAgent.Count == 0)
            http.DefaultRequestHeaders.UserAgent.ParseAdd($"MCUGLauncher/{ProductInfo.Version}");
    }

    /// <summary>
    /// 读取。
    /// </summary>
    public MuaCredential? Read()
    {
        try
        {
            var node = storage.ReadAsJsonNode();
            if (node == null || node["AccessToken"] == null)
                return null;
            var saved = node.Deserialize<MuaCredential>() ?? throw new AccountStorageException("MUA 账号缓存无效，原文件已保留。");
            Validate(saved);
            return saved;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            throw new AccountStorageException("MUA 账号缓存结构无效，原文件已保留，请重新登录。", ex);
        }
    }

    static void Validate(MuaCredential c)
    {
        if (c.Server != Api || c.Profile == null || !Regex.IsMatch(c.Profile.Id ?? "", "^[a-fA-F0-9]{32}$") || !Regex.IsMatch(c.Profile.Name ?? "", "^[A-Za-z0-9_]{1,16}$") || string.IsNullOrWhiteSpace(c.AccessToken) || c.AccessToken.Length > 8192 || string.IsNullOrWhiteSpace(c.ClientToken) || c.ClientToken.Length > 1024 || c.Account == null || c.Account.Length > 320)
            throw new AccountStorageException("MUA 账号缓存无效，原文件已保留，请重新登录。");
    }

    void Save(MuaCredential credential)
    {
        Validate(credential);
        storage.Write(JsonSerializer.SerializeToNode(credential)!, null);
    }

    static MuaProfile Profile(JsonElement value)
    {
        var result = new MuaProfile(value.GetProperty("id").GetString() ?? "", value.GetProperty("name").GetString() ?? "");
        if (!Regex.IsMatch(result.Id, "^[a-fA-F0-9]{32}$") || !Regex.IsMatch(result.Name, "^[A-Za-z0-9_]{1,16}$"))
            throw new IOException("MUA 返回的角色资料无效。");
        return result;
    }

    static string Required(JsonElement value, string name, int max)
    {
        var text = value.GetProperty(name).GetString();
        if (string.IsNullOrWhiteSpace(text) || text.Length > max)
            throw new IOException("MUA 返回的会话无效。");
        return text;
    }

    async Task<HttpResponseMessage> Send(string path, object? body, CancellationToken token)
    {
        using var request = new HttpRequestMessage(body == null ? HttpMethod.Get : HttpMethod.Post, new Uri(new Uri(Api), path));
        if (body != null)
            request.Content = JsonContent.Create(body);
        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            throw new IOException("MUA 连接超时，请检查网络后重试。");
        }
        // Credential requests never follow redirects or reuse a token on another authentication origin.
        if ((int)response.StatusCode >= 300 && (int)response.StatusCode < 400)
        {
            response.Dispose();
            throw new IOException("MUA 认证地址发生跳转，已停止发送凭据，请稍后重试。");
        }

        return response;
    }

    static Exception Failure(HttpStatusCode code) => code switch
    {
        HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized => new AccountLoginException("MUA 登录信息或会话已失效，请重新登录。", true),
        HttpStatusCode.TooManyRequests => new IOException("MUA 请求过于频繁，请稍后重试。"),
        _ => new IOException("MUA 服务暂不可用（HTTP " + (int)code + "），请稍后重试。")
    };

    static async Task<string> Text(HttpResponseMessage response, CancellationToken token)
    {
        if (!response.IsSuccessStatusCode)
            throw Failure(response.StatusCode);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(25));
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var output = new MemoryStream();
            var buffer = new byte[8192];
            int count;
            while ((count = await stream.ReadAsync(buffer, timeout.Token)) > 0)
            {
                if (output.Length + count > 1024 * 1024)
                    throw new IOException("MUA 响应过大。");
                output.Write(buffer, 0, count);
            }

            return Encoding.UTF8.GetString(output.ToArray());
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            throw new IOException("读取 MUA 响应超时，请稍后重试。");
        }
    }

    /// <summary>
    /// 登录。
    /// </summary>
    /// <param name="account">MUA 账号</param>
    /// <param name="password">密码</param>
    /// <param name="select">多角色时选择具体档案的回调</param>
    /// <param name="token">取消令牌</param>
    /// <returns>已保存的 MUA 凭据</returns>
    public async Task<MuaCredential> Login(string account, string password, Func<IReadOnlyList<MuaProfile>, Task<MuaProfile?>> select, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(account) || account.Length > 320 || password.Length == 0 || password.Length > 4096)
            throw new IOException("请填写有效的 MUA 账号和密码。");
        await gate.WaitAsync(token);
        try
        {
            var clientToken = Guid.NewGuid().ToString("N");
            using var response = await Send("authserver/authenticate", new
            {
                username = account.Trim(),
                password,
                clientToken,
                requestUser = true,
                agent = new
                {
                    name = "Minecraft",
                    version = 1
                }
            }, token);
            using var json = JsonDocument.Parse(await Text(response, token));
            var root = json.RootElement;
            var credential = new MuaCredential
            {
                Account = account.Trim(),
                AccessToken = Required(root, "accessToken", 8192),
                ClientToken = Required(root, "clientToken", 1024)
            };
            if (credential.ClientToken != clientToken)
                throw new IOException("MUA 返回了不匹配的客户端会话。");
            if (root.TryGetProperty("selectedProfile", out var bound) && bound.ValueKind == JsonValueKind.Object)
                credential.Profile = Profile(bound);
            else
            {
                var profiles = root.TryGetProperty("availableProfiles", out var list) && list.ValueKind == JsonValueKind.Array ? list.EnumerateArray().Take(101).Select(Profile).ToList() : [];
                if (profiles.Count == 0)
                    throw new IOException("此 MUA 账号没有游戏角色，请先到用户中心的「角色管理」创建角色。");
                if (profiles.Count > 100)
                    throw new IOException("MUA 返回的角色数量超出限制。");
                var selected = profiles.Count == 1 ? profiles[0] : await select(profiles);
                if (selected == null)
                    throw new OperationCanceledException(token);
                if (!profiles.Contains(selected))
                    throw new IOException("所选角色不属于此 MUA 账号。");
                credential = await Refresh(credential, selected, token);
            }

            token.ThrowIfCancellationRequested();
            Save(credential);
            return credential;
        }
        catch (JsonException)
        {
            throw new IOException("MUA 返回了无法识别的认证数据。");
        }
        finally
        {
            gate.Release();
        }
    }

    async Task<MuaCredential> Refresh(MuaCredential saved, MuaProfile? selection, CancellationToken token)
    {
        object body = selection == null ? new
        {
            accessToken = saved.AccessToken,
            clientToken = saved.ClientToken,
            requestUser = true
        } : (object)new
        {
            accessToken = saved.AccessToken,
            clientToken = saved.ClientToken,
            requestUser = true,
            selectedProfile = new
            {
                id = selection.Id,
                name = selection.Name
            }
        };
        using var response = await Send("authserver/refresh", body, token);
        using var json = JsonDocument.Parse(await Text(response, token));
        var root = json.RootElement;
        var profile = Profile(root.GetProperty("selectedProfile"));
        var expected = selection ?? saved.Profile;
        if (!profile.Id.Equals(expected.Id, StringComparison.OrdinalIgnoreCase))
            throw new IOException("MUA 刷新后的角色 UUID 不匹配，已停止启动，请重新登录。");
        var client = Required(root, "clientToken", 1024);
        if (client != saved.ClientToken)
            throw new IOException("MUA 刷新后的客户端会话不匹配。");
        return new()
        {
            Account = saved.Account,
            AccessToken = Required(root, "accessToken", 8192),
            ClientToken = client,
            Profile = profile
        };
    }

    /// <summary>
    /// 确保。
    /// </summary>
    public async Task<MuaCredential> Ensure(bool forceRefresh, CancellationToken token)
    {
        await gate.WaitAsync(token);
        try
        {
            var saved = Read() ?? throw new AccountLoginException("请先登录 MUA 账号。", true);
            if (!forceRefresh)
            {
                using var response = await Send("authserver/validate", new
                {
                    accessToken = saved.AccessToken,
                    clientToken = saved.ClientToken
                }, token);
                if (response.StatusCode == HttpStatusCode.NoContent)
                    return saved;
                if (response.StatusCode is not (HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized))
                    throw Failure(response.StatusCode);
            }

            var refreshed = await Refresh(saved, null, token);
            token.ThrowIfCancellationRequested();
            Save(refreshed);
            return refreshed;
        }
        catch (JsonException)
        {
            throw new IOException("MUA 返回了无法识别的认证数据。");
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// 执行 Forget。
    /// </summary>
    public async Task<bool> Forget(CancellationToken token)
    {
        await gate.WaitAsync(token);
        try
        {
            bool revoked = true;
            try
            {
                var saved = Read();
                if (saved != null)
                {
                    using var response = await Send("authserver/invalidate", new
                    {
                        accessToken = saved.AccessToken,
                        clientToken = saved.ClientToken
                    }, token);
                    revoked = response.StatusCode == HttpStatusCode.NoContent;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or OperationCanceledException or AccountStorageException)
            {
                revoked = false;
            }

            storage.Forget();
            return revoked;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// 执行 Metadata。
    /// </summary>
    public async Task<MuaMetadata> Metadata(CancellationToken token)
    {
        using var response = await Send("", null, token);
        var text = await Text(response, token);
        using var json = JsonDocument.Parse(text);
        var root = json.RootElement;
        if (!root.TryGetProperty("signaturePublickey", out var key) || !key.GetString()!.Contains("BEGIN PUBLIC KEY"))
            throw new IOException("MUA 认证元数据无效。");
        var domains = root.GetProperty("skinDomains").EnumerateArray().Take(200).Select(x => x.GetString() ?? "").Where(x => x.Length > 0 && x.Length < 254).ToList();
        return new(text, domains);
    }

    /// <summary>
    /// 执行 皮肤。
    /// </summary>
    public async Task<Uri?> Skin(MuaCredential saved, MuaMetadata metadata, CancellationToken token)
    {
        Validate(saved);
        using var response = await Send("sessionserver/session/minecraft/profile/" + saved.Profile.Id + "?unsigned=false", null, token);
        if (response.StatusCode == HttpStatusCode.NoContent)
            return null;
        using var profile = JsonDocument.Parse(await Text(response, token));
        if (!profile.RootElement.GetProperty("id").GetString()!.Equals(saved.Profile.Id, StringComparison.OrdinalIgnoreCase))
            throw new IOException("MUA 皮肤角色不匹配。");
        foreach (var property in profile.RootElement.GetProperty("properties").EnumerateArray())
        {
            if (property.GetProperty("name").GetString() != "textures")
                continue;
            using var textures = JsonDocument.Parse(Convert.FromBase64String(property.GetProperty("value").GetString()!));
            if (!textures.RootElement.GetProperty("textures").TryGetProperty("SKIN", out var skin))
                return null;
            var uri = new Uri(skin.GetProperty("url").GetString()!);
            if (uri.Scheme != "https" || uri.UserInfo.Length > 0 || !metadata.SkinDomains.Any(d => uri.Host.Equals(d, StringComparison.OrdinalIgnoreCase) || d.StartsWith('.') && uri.Host.EndsWith(d, StringComparison.OrdinalIgnoreCase)))
                throw new IOException("MUA 皮肤来源不在认证站声明的 HTTPS 域名内。");
            return uri;
        }

        return null;
    }

    /// <summary>
    /// 执行 会话。
    /// </summary>
    public static MSession Session(MuaCredential c)
    {
        Validate(c);
        return new MSession
        {
            Username = c.Profile.Name,
            UUID = c.Profile.Id,
            AccessToken = c.AccessToken,
            ClientToken = c.ClientToken,
            UserType = "mojang"
        };
    }

    /// <summary>
    /// 执行 Dispose。
    /// </summary>
    public void Dispose()
    {
        if (ownsHttp)
            http.Dispose();
        gate.Dispose();
    }
}
