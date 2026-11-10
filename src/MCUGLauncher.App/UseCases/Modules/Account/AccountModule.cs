using System.Text.Json;
using BlockLaunch;
using CmlLib.Core.Auth;
using MCUGLauncher.Contracts;
using MCUGLauncher.Services;

namespace MCUGLauncher.UseCases;

/// <summary>账号领域模块：登录、账号切换、MUA 档案、头像皮肤、账号状态与事件。</summary>
internal sealed class AccountModule : IAccountStateWriter, IAccountStateProvider, IAccountSessionProvider, IAccountSessionService
{
    readonly IAccountFeatureInvoker invoker;
    readonly IAccountContext context;

    public AccountModule(IAccountFeatureInvoker invoker, IAccountContext context)
    {
        this.invoker = invoker;
        this.context = context;
        State = new AccountStateInfo(
            context.Config.Player,
            context.Config.AccountType,
            context.Config.OfflinePlayer,
            context.OnlineSession != null || context.Config.AccountType == "offline",
            context.AccountRevision);
        Session = context.OnlineSession;
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
        var player = mode == "offline" ? context.Config.OfflinePlayer : identity?.Player ?? session?.Username;
        if (string.IsNullOrWhiteSpace(player))
            throw new AccountLoginException("账号没有有效的玩家名，请重新登录。", true);
        if (context.Config.AccountType == "offline")
            context.Config.OfflinePlayer = context.Config.Player;
        if (session != null && verified)
            context.AccountSessions.Remember(mode, session);
        context.OnlineSession = session;
        context.Config.AccountType = mode;
        context.Config.Player = player;
        if (persist)
            context.Store.Save(context.Config);
        context.SendAccountState();
        Session = session;
        Publish(new AccountStateInfo(
            player,
            mode,
            context.Config.OfflinePlayer,
            session != null || mode == "offline",
            context.AccountRevision));
    }

    public async Task<MSession> PrepareLaunchSessionAsync()
    {
        var mode = context.Config.AccountType;
        var player = context.Config.Player;
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

        var session = await new AccountModeCommands(context).EnsureFreshAsync(mode);
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
                var state = AccountStateInfo.From(root) with
                {
                    OfflinePlayer = context.Config.OfflinePlayer,
                    Authenticated = context.OnlineSession != null || context.Config.AccountType == "offline",
                    Revision = context.AccountRevision
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
