using MCUGLauncher.Services;
using CmlLib.Core.Auth;
using MCUGLauncher.Contracts;

namespace MCUGLauncher.UseCases;

/// <summary>账号状态的只读视图，供状态构建与启动流程使用。</summary>
internal interface IAccountStateProvider
{
    AccountStateInfo? AccountState
    {
        get;
    }
    event Action<AccountStateInfo>? AccountStateChanged;
}

internal interface IAccountSessionProvider
{
    MSession? Session
    {
        get;
    }
}

/// <summary>启动流程的账号会话入口；离线与在线会话都由账号模块准备。</summary>
/// <summary>会话写入端口；账号模块是唯一持有者。</summary>
internal interface IAccountSessionStore
{
    MSession? Current
    {
        get;
    }
    void Set(MSession? session);
}

internal interface IAccountSessionService
{
    Task<MSession> PrepareLaunchSessionAsync();
}

/// <summary>账号事件/状态通知端口。</summary>
internal interface IAccountNotifier
{
    void Send(object payload);
    void SendAccountState();
}

internal interface IAccountLogger
{
    void Log(string message);
}

internal sealed class AccountNotifier(Action<object> send, Action sendState) : IAccountNotifier
{
    public void Send(object payload) => send(payload);
    public void SendAccountState() => sendState();
}

internal sealed class AccountServices(
    IAccountAuthGateway auth,
    IAccountStore store,
    IAccountSessionCache sessions,
    IAccountSessionStore sessionStore,
    IAccountRevision revision,
    IAccountNotifier notifier,
    IAccountLogger logger,
    CancellationToken token)
{
    public IAccountAuthGateway Auth { get; } = auth;
    public IAccountStore Store { get; } = store;
    public IAccountSessionCache Sessions { get; } = sessions;
    public IAccountSessionStore SessionStore { get; } = sessionStore;
    public IAccountRevision Revision { get; } = revision;
    public IAccountNotifier Notifier { get; } = notifier;
    public IAccountLogger Logger { get; } = logger;
    public CancellationToken Token { get; } = token;
    public AccountRecord Record => Store.Load();
}

/// <summary>账号并发版本号端口。</summary>
internal interface IAccountRevision
{
    long Current
    {
        get;
    }
    long Next();
}

/// <summary>账号会话缓存端口。</summary>
internal interface IAccountSessionCache
{
    MSession? Find(string mode);
    MSession? Fresh(string mode);
    AccountIdentity? Identity(string mode);
    void Remember(string mode, MSession session);
    void RememberIdentity(string mode, AccountIdentity identity);
    void Forget(string mode);
}
