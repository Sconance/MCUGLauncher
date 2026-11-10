using BlockLaunch;
using CmlLib.Core.Auth;
using MCUGLauncher.Services;

namespace MCUGLauncher.UseCases;

/// <summary>账号状态写入端口；由账号模块实现，命令通过它提交切换结果。</summary>
internal interface IAccountStateWriter
{
    void Apply(
        string mode,
        MSession? session,
        bool verified = true,
        bool persist = true,
        AccountIdentity? identity = null);
}
