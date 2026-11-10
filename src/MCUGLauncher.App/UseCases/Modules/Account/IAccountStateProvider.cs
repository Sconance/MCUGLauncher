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
    void SendAccountState();
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
    void Remember(string mode, MSession session);
}
