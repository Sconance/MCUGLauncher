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
