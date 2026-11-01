using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using CmlLib.Core.Auth;
using CmlLib.Core.Auth.Microsoft;
using CmlLib.Core.Auth.Microsoft.Sessions;
using XboxAuthNet.Game.Accounts;
using XboxAuthNet.Game;
using XboxAuthNet.Game.Authenticators;

namespace BlockLaunch;

/// <summary>
/// 表示 SavedMicrosoft账号 数据。
/// </summary>
public record SavedMicrosoftAccount(string Username, string Uuid, string SkinUrl = "");

/// <summary>
/// 提供 Microsoft账号Service 相关功能。
/// </summary>
public sealed class MicrosoftAccountService
{
    readonly ProtectedAccountStorage storage;
    readonly HttpClient http;
    readonly string directory;
    readonly XboxAuthNet.OAuth.CodeFlow.IWebUI? browser;

    public MicrosoftAccountService(string directory, HttpClient client, XboxAuthNet.OAuth.CodeFlow.IWebUI? browser = null)
    {
        this.directory = directory;
        Directory.CreateDirectory(directory);
        http = client;
        this.browser = browser;
        storage = new(Path.Combine(directory, "microsoft-account.dat"));
    }

    JsonXboxGameAccountManager Manager(XboxAuthNet.Game.Accounts.JsonStorage.IJsonStorage store) => new(store, JEGameAccount.FromSessionStorage, JsonXboxGameAccountManager.DefaultSerializerOption);

    /// <summary>
    /// 读取Saved账号。
    /// </summary>
    public SavedMicrosoftAccount? ReadSavedAccount()
    {
        var account = Manager(storage).GetAccounts().OfType<JEGameAccount>().FirstOrDefault(a => a.Profile != null);
        return account?.Profile is
        {
            Username:
            {
                Length: > 0
            } name, UUID:
            {
                Length: > 0
            } uuid
        } profile ? new(name, uuid, profile.Skins.FirstOrDefault(s => s.State == "ACTIVE")?.Url ?? "") : null;
    }

    FileStream LockAccount()
    {
        try
        {
            return new(Path.Combine(directory, "microsoft-account.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException)
        {
            throw new AccountLoginException("另一个启动器正在修改账号，请稍后再试。");
        }
    }

    /// <summary>
    /// 执行 Authenticate。
    /// </summary>
    /// <param name="interactive">true 时打开系统浏览器授权，false 时静默刷新</param>
    /// <param name="token">取消令牌</param>
    /// <returns>已通过校验的微软会话</returns>
    public async Task<MSession> Authenticate(bool interactive, CancellationToken token)
    {
        using var lease = LockAccount();
        token.ThrowIfCancellationRequested();
        // An interactive attempt uses a fresh in-memory account. Failed/cancelled attempts never replace a saved account.
        var pending = new MemoryAccountStorage();
        var manager = Manager(interactive ? pending : storage);
        var cached = manager.GetAccounts();
        // Initialize lazy storage before NewAccount; otherwise its first save discards the new entry.
        if (!interactive && !cached.Any())
            throw new AccountLoginException("尚未登录微软账号，请先完成正版登录。", true);
        if (interactive && browser == null)
            throw new AccountLoginException("尚未配置登录窗口，请重新打开启动器。", true);

        try
        {
            var handler = new JELoginHandlerBuilder().WithHttpClient(http).WithAccountManager(manager).Build();
            var account = interactive ? manager.NewAccount() : manager.GetDefaultAccount();
            var authenticator = handler.CreateAuthenticator(account, token);
            if (interactive)
                authenticator.AddForceMicrosoftOAuthForJE(oauth => oauth.Interactive(flow => flow.WithWebUI(browser!)));
            else
                authenticator.AddMicrosoftOAuthForJE(oauth => oauth.Silent());
            // Full Xbox flow supports the account/family checks omitted by the old basic-only path.
            authenticator.AddXboxAuthForJE(xbox => xbox.Full());
            authenticator.AddForceJEAuthenticator(je => je.Build());
            authenticator.AddAuthenticatorWithoutValidator(new MinecraftOwnershipGuard());
            var session = await authenticator.ExecuteForLauncherAsync();
            token.ThrowIfCancellationRequested();
            ValidateSession(session);
            if (interactive)
                storage.Write(pending.Value ?? throw new AccountStorageException("登录信息未能生成，原登录已保留。"), null);
            return session;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (AccountStorageException)
        {
            throw;
        }
        catch (AccountLoginException)
        {
            throw;
        }
        catch (Exception ex)
        {
            token.ThrowIfCancellationRequested();
            throw Translate(ex, !interactive);
        }
    }

    /// <summary>
    /// 执行 Forget。
    /// </summary>
    public void Forget()
    {
        using var lease = LockAccount();
        storage.Forget();
    }

    /// <summary>
    /// 执行 Validate会话。
    /// </summary>
    public static void ValidateSession(MSession session)
    {
        var token = new JEToken
        {
            AccessToken = session.AccessToken,
            ExpiresOn = DateTime.UtcNow.AddMinutes(1)
        };
        if (session.UserType != "msa" || !Guid.TryParse(session.UUID, out _) || !Regex.IsMatch(session.Username ?? "", "^[A-Za-z0-9_]{3,16}$") || !token.Validate())
            throw new AccountLoginException("微软授权未返回有效的 Java 版玩家档案，无法用于正版启动。");
    }

    /// <summary>
    /// 执行 Translate。
    /// </summary>
    public static AccountLoginException Translate(Exception error, bool refreshing)
    {
        var errors = new List<Exception>();
        for (var ex = error; ex != null && errors.Count < 12; ex = ex.InnerException)
            errors.Add(ex);
        // Inspect only for classification. Never show or log raw authentication payloads, URLs or exception bodies.
        var text = string.Join(" ", errors.Select(e => e.GetType().Name + " " + e.Message));
        if (text.Contains("2148916233"))
            return new("此微软账号尚未建立 Xbox 档案。请在 xbox.com 完成档案设置后重试。");
        if (text.Contains("2148916238"))
            return new("Xbox 拒绝了家庭 / 未成年账号授权。请由监护人在微软家庭设置中处理后重试。");
        if (text.Contains("GameOwnership", StringComparison.OrdinalIgnoreCase) || text.Contains("GameNotOwned", StringComparison.OrdinalIgnoreCase) || text.Contains("doesn't own", StringComparison.OrdinalIgnoreCase) || text.Contains("JEProfile", StringComparison.OrdinalIgnoreCase) && text.Contains("404"))
            return new("未取得 Minecraft Java 版授权或玩家档案。请确认账号拥有 Java 版或有效订阅，并已创建 Java 版档案。");
        if (errors.OfType<HttpRequestException>().Any(e => e.StatusCode is HttpStatusCode.TooManyRequests))
            return new("微软 / Xbox 服务暂时限流，请稍后再试。");
        if (text.Contains("invalid_grant", StringComparison.OrdinalIgnoreCase) || text.Contains("interaction_required", StringComparison.OrdinalIgnoreCase) || text.Contains("consent_required", StringComparison.OrdinalIgnoreCase))
            return new("微软登录授权需要重新确认，请在官方窗口重新登录。", true);
        if (errors.Any(e => e is HttpRequestException or TaskCanceledException))
            return new("登录服务连接失败或超时，请检查网络和代理后重试。已保存账号未主动清除。");
        if (text.Contains("cancel", StringComparison.OrdinalIgnoreCase) || text.Contains("access_denied", StringComparison.OrdinalIgnoreCase))
            return new("登录窗口已关闭或授权被取消，未完成新的登录。");
        if (errors.Any(e => e is IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException))
            return new("账号缓存无法保存或读取，请检查本地目录权限后重试。");
        return refreshing ? new("保存的授权已失效或无法刷新，请重新进行微软登录。", true) : new("微软 / Xbox / Minecraft 授权未完成。请确认账号授权、网络及 Java 版购买状态后重试。");
    }
}
