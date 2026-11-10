using BlockLaunch;
using CmlLib.Core.Auth;
using MCUGLauncher.Models;
using MCUGLauncher.Services;

namespace MCUGLauncher.UseCases;

/// <summary>账号模块所需的最小上下文；避免直接依赖完整 LauncherContext。</summary>
internal interface IAccountContext
{
    AppConfig Config
    {
        get;
    }
    ConfigStore Store
    {
        get;
    }
    MicrosoftAccountService? Microsoft
    {
        get;
    }
    MuaAccountService? Mua
    {
        get;
    }
    AccountSessionCache AccountSessions
    {
        get;
    }
    MSession? OnlineSession
    {
        get; set;
    }
    CancellationToken Token
    {
        get;
    }
    long AccountRevision
    {
        get;
    }
    long NextRevision();
    IAccountStateWriter StateWriter
    {
        get;
    }
    Action<object> Send
    {
        get;
    }
    void SendAccountState();
    void Log(string message);
}
