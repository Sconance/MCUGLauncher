using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using CmlLib.Core.Auth.Microsoft.Sessions;
using XboxAuthNet.Game.Authenticators;

namespace BlockLaunch;

/// <summary>
/// 提供 MinecraftOwnershipGuard 相关功能。
/// </summary>
public sealed class MinecraftOwnershipGuard : IAuthenticator
{
    /// <summary>
    /// 执行 Execute。
    /// </summary>
    public async ValueTask ExecuteAsync(AuthenticateContext context)
    {
        var access = JETokenSource.Default.Get(context.SessionStorage)?.AccessToken;
        if (string.IsNullOrWhiteSpace(access))
            throw new AccountLoginException("Minecraft 授权令牌为空，请重新登录。", true);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.minecraftservices.com/entitlements/mcstore");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        using var response = await context.HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.CancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized)
            throw new AccountLoginException("Minecraft 授权已失效，请重新登录。", true);
        if (response.StatusCode is HttpStatusCode.TooManyRequests)
            throw new AccountLoginException("Minecraft 服务限流，请稍后重试。");
        if (!response.IsSuccessStatusCode)
            throw new AccountLoginException("无法确认 Minecraft Java 版授权，服务暂时不可用，请稍后重试。");
        await using var stream = await response.Content.ReadAsStreamAsync(context.CancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: context.CancellationToken);
        if (!doc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array || !items.EnumerateArray().Any(e => e.TryGetProperty("name", out var name) && name.GetString() is "game_minecraft" or "product_minecraft"))
            throw new AccountLoginException("此账号未返回 Minecraft Java 版授权。请确认购买或订阅有效，并已创建 Java 版玩家档案。");
    }
}
