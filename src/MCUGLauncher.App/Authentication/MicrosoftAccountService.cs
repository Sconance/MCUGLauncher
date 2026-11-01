using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using CmlLib.Core.Auth;
using CmlLib.Core.Auth.Microsoft;
using CmlLib.Core.Auth.Microsoft.Sessions;
using XboxAuthNet.Game.Accounts;
using XboxAuthNet.Game.OAuth;
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
    readonly SemaphoreSlim authenticationGate = new(1, 1);
    readonly Action<string>? diagnostic;
    readonly HttpClient http;
    readonly string directory;
    readonly XboxAuthNet.OAuth.CodeFlow.IWebUI? browser;

    public MicrosoftAccountService(string directory, HttpClient client, XboxAuthNet.OAuth.CodeFlow.IWebUI? browser = null, Action<string>? diagnostic = null)
    {
        this.directory = directory;
        Directory.CreateDirectory(directory);
        http = client;
        this.browser = browser;
        this.diagnostic = diagnostic;
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
    /// <param name="interactive">true 时使用注入的登录窗口授权，false 时静默刷新</param>
    /// <param name="token">取消令牌</param>
    /// <returns>已通过校验的微软会话</returns>
    public async Task<MSession> Authenticate(bool interactive, CancellationToken token)
    {
        await authenticationGate.WaitAsync(token);
        try
        {
            return await AuthenticateCore(interactive, token);
        }
        finally { authenticationGate.Release(); }
    }

    async Task<MSession> AuthenticateCore(bool interactive, CancellationToken token)
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

        var stage = "OAuth";
        void Stage(string value)
        {
            stage = value;
            diagnostic?.Invoke("MicrosoftAuth phase=" + value);
        }
        try
        {
            var handler = new JELoginHandlerBuilder().WithHttpClient(http).WithAccountManager(manager).Build();
            var account = interactive ? manager.NewAccount() : manager.GetDefaultAccount();
            if (!interactive && MicrosoftOAuthSessionSource.Default.Get(account.SessionStorage) == null)
                throw new AccountLoginException("保存的账号缺少微软授权，请重新登录。", true);
            var authenticator = handler.CreateAuthenticator(account, token);
            Stage("OAuth");
            if (interactive)
                authenticator.AddForceMicrosoftOAuthForJE(oauth => oauth.Interactive(flow => flow.WithWebUI(browser!)));
            else
                authenticator.AddMicrosoftOAuthForJE(oauth => oauth.Silent());
            authenticator.AddAuthenticatorWithoutValidator(new AuthenticationCheckpoint(() => Stage("Xbox")));
            // Full Xbox flow supports the account/family checks omitted by the old basic-only path.
            authenticator.AddXboxAuthForJE(xbox => xbox.Full());
            authenticator.AddAuthenticatorWithoutValidator(new AuthenticationCheckpoint(() => Stage("Minecraft")));
            if (interactive)
                authenticator.AddForceJEAuthenticator(je => je.Build());
            else
                authenticator.AddJEAuthenticator();
            authenticator.AddAuthenticatorWithoutValidator(new AuthenticationCheckpoint(() => Stage("Ownership")));
            authenticator.AddAuthenticatorWithoutValidator(new MinecraftOwnershipGuard());
            var session = await authenticator.ExecuteForLauncherAsync();
            token.ThrowIfCancellationRequested();
            ValidateSession(session);
            if (interactive)
                storage.Write(pending.Value ?? throw new AccountStorageException("登录信息未能生成，原登录已保留。"), null);
            Stage("Completed");
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
        catch (AccountLoginException error)
        {
            diagnostic?.Invoke("MicrosoftAuth phase=" + stage + " failure=AccountLoginException requiresLogin=" + error.RequiresInteractive);
            throw;
        }
        catch (Exception ex)
        {
            token.ThrowIfCancellationRequested();
            diagnostic?.Invoke("MicrosoftAuth phase=" + stage + " failure=" + string.Join(",", Flatten(ex).Select(e => e.GetType().Name).Distinct()));
            throw Translate(ex, !interactive, stage);
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
    public static AccountLoginException Translate(Exception error, bool refreshing, string? stage = null)
    {
        var errors = Flatten(error);
        var known = errors.OfType<AccountLoginException>().FirstOrDefault();
        if (known != null)
            return known;
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
        var phase = stage switch
        {
            "OAuth" => "微软",
            "Xbox" => "Xbox",
            "Minecraft" => "Minecraft",
            "Ownership" => "正版资格",
            _ => "登录服务"
        };
        return new(phase + (refreshing ? "授权恢复" : "登录") + "暂未完成，请稍后重试。已保存授权仍然保留；此提示不代表账号没有购买游戏。");
    }
    static List<Exception> Flatten(Exception error)
    {
        var pending = new Queue<Exception>();
        var errors = new List<Exception>();
        pending.Enqueue(error);
        while (pending.Count > 0 && errors.Count < 32)
        {
            var item = pending.Dequeue();
            if (errors.Contains(item))
                continue;
            errors.Add(item);
            if (item.InnerException != null)
                pending.Enqueue(item.InnerException);
            if (item is AggregateException aggregate)
                foreach (var child in aggregate.InnerExceptions)
                    pending.Enqueue(child);
        }
        return errors;
    }

}
