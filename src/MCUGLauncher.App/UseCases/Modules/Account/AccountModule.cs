using System.Text.Json;
using BlockLaunch;
using CmlLib.Core.Auth;
using MCUGLauncher.Contracts;
using MCUGLauncher.Services;

namespace MCUGLauncher.UseCases;

/// <summary>账号领域模块：登录、账号切换、MUA 档案、头像皮肤、账号状态与事件。</summary>
internal sealed class AccountModule : IAccountStateWriter, IAccountStateProvider, IAccountSessionProvider, IAccountSessionStore, IAccountSessionService
{
    readonly IAccountFeatureInvoker invoker;
    readonly IAccountStore store;
    readonly IAccountSessionCache sessionCache;
    readonly IAccountNotifier notifier;
    readonly IAccountRevision revision;
    readonly Func<string, Task<MSession>> refreshSession;

    public AccountModule(
        IAccountFeatureInvoker invoker,
        IAccountStore store,
        IAccountSessionCache sessionCache,
        IAccountNotifier notifier,
        IAccountRevision revision,
        Func<string, Task<MSession>> refreshSession)
    {
        this.invoker = invoker;
        this.store = store;
        this.sessionCache = sessionCache;
        this.notifier = notifier;
        this.revision = revision;
        this.refreshSession = refreshSession;
        var record = store.Load();
        State = new AccountStateInfo(
            record.Player,
            record.Mode,
            record.OfflinePlayer,
            record.Mode == "offline",
            revision.Current);
    }

    public AccountStateInfo? State
    {
        get; private set;
    }
    public AccountStateInfo? AccountState => State;
    public MSession? Session
    {
        get; private set;
    }
    public MSession? Current => Session;
    public void Set(MSession? session) => Session = session;

    public event Action<AuthResultInfo>? AuthResultReceived;
    public event Action<AccountStateInfo>? AccountStateChanged;
    public event Action<AccountSwitchResultInfo>? AccountSwitchResultReceived;
    public event Action<MuaProfilesInfo>? MuaProfilesReceived;

    public void Apply(
        string mode,
        MSession? session,
        bool verified = true,
        bool persist = true,
        AccountIdentity? identity = null)
    {
        var record = store.Load();
        var player = mode == "offline" ? record.OfflinePlayer : identity?.Player ?? session?.Username;
        if (string.IsNullOrWhiteSpace(player))
            throw new AccountLoginException("账号没有有效的玩家名，请重新登录。", true);
        if (record.Mode == "offline")
            record = record with
            {
                OfflinePlayer = record.Player
            };
        if (session != null && verified)
            sessionCache.Remember(mode, session);
        Session = session;
        record = new AccountRecord(mode, player, record.OfflinePlayer);
        store.Save(record, persist);
        notifier.SendAccountState();
        Publish(new AccountStateInfo(
            player,
            mode,
            record.OfflinePlayer,
            session != null || mode == "offline",
            revision.Current));
    }

    public async Task<MSession> PrepareLaunchSessionAsync()
    {
        var record = store.Load();
        var mode = record.Mode;
        var player = record.Player;
        if (mode == "offline")
        {
            var offline = new MSession
            {
                Username = player,
                UUID = ProtocolValues.OfflineUuid(player),
                AccessToken = "0",
                ClientToken = Guid.NewGuid().ToString("N"),
                UserType = "legacy"
            };
            Session = offline;
            return offline;
        }

        var session = await refreshSession(mode);
        if (!string.Equals(session.Username, player, StringComparison.Ordinal))
            throw new IOException("账号身份已改变，请重新选择账号后启动。");
        Session = session;
        return session;
    }

    public Task<AuthResultInfo> LoginMicrosoftAsync() =>
        invoker.InvokeAsync<AuthResultInfo>(nameof(LoginMicrosoftAsync), new
        {
        });

    public Task<AuthResultInfo> LoginMuaAsync(string account, string password) =>
        invoker.InvokeAsync<AuthResultInfo>(nameof(LoginMuaAsync), new
        {
            account,
            password
        });

    public Task<AccountSwitchResultInfo> SwitchAccountModeAsync(string mode) =>
        invoker.InvokeAsync<AccountSwitchResultInfo>(nameof(SwitchAccountModeAsync), new
        {
            mode
        });

    public Task<AuthResultInfo> ForgetAccountAsync(string mode) =>
        invoker.InvokeAsync<AuthResultInfo>(nameof(ForgetAccountAsync), new
        {
            mode
        });

    public Task<AuthResultInfo> LogoutAccountAsync() =>
        invoker.InvokeAsync<AuthResultInfo>(nameof(LogoutAccountAsync), new
        {
        });

    public Task<SavedInfo> SelectMuaProfileAsync(string token, string profileId) =>
        invoker.InvokeAsync<SavedInfo>(nameof(SelectMuaProfileAsync), new
        {
            token,
            profileId
        });

    public Task<AvatarInfo?> ReadAvatarAsync() =>
        invoker.InvokeAsync<AvatarInfo?>(nameof(ReadAvatarAsync), new
        {
        });

    public Task<AvatarInfo?> SetOfflineSkinAsync(string path) =>
        invoker.InvokeAsync<AvatarInfo?>(nameof(SetOfflineSkinAsync), new
        {
            path
        });

    /// <summary>处理账号相关消息；非账号消息返回 false，由 LauncherService 继续分发。</summary>
    public bool Handle(string type, JsonElement root)
    {
        switch (type)
        {
            case "authResult":
                AuthResultReceived?.Invoke(AuthResultInfo.From(root));
                return true;
            case "accountState":
                var incoming = AccountStateInfo.From(root);
                var account = store.Load();
                var state = incoming with
                {
                    OfflinePlayer = account.OfflinePlayer,
                    Authenticated = Session != null || incoming.AccountType == "offline",
                    Revision = revision.Current
                };
                Publish(state);
                AccountStateChanged?.Invoke(state);
                return true;
            case "accountSwitchResult":
                AccountSwitchResultReceived?.Invoke(AccountSwitchResultInfo.From(root));
                return true;
            case "muaProfiles":
                MuaProfilesReceived?.Invoke(MuaProfilesInfo.From(root));
                return true;
            default:
                return false;
        }
    }

    void Publish(AccountStateInfo state) => State = state;
}
