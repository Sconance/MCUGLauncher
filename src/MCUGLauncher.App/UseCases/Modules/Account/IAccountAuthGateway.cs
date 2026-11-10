using BlockLaunch;
using MCUGLauncher.Services;
using CmlLib.Core.Auth;

namespace MCUGLauncher.UseCases;

internal interface IAccountAuthGateway
{
    bool HasSavedMicrosoftAccount
    {
        get;
    }
    AccountIdentity? ReadMicrosoftIdentity();
    AccountIdentity? ReadMuaIdentity();
    Task<MSession> AuthenticateMicrosoftAsync(bool interactive, CancellationToken token);
    Task<MSession> LoginMuaAsync(
        string account,
        string password,
        Func<IReadOnlyList<MuaProfile>, Task<MuaProfile?>> choose,
        CancellationToken token);
    Task<MSession> EnsureMuaAsync(bool interactive, CancellationToken token);
    void ForgetMicrosoft();
    Task ForgetMuaAsync(CancellationToken token);
}

internal sealed class MicrosoftMuaAuthGateway(
    MicrosoftAccountService? microsoft,
    MuaAccountService? mua) : IAccountAuthGateway
{
    public bool HasSavedMicrosoftAccount => microsoft?.ReadSavedAccount() != null;

    public AccountIdentity? ReadMicrosoftIdentity()
    {
        var saved = microsoft?.ReadSavedAccount();
        return saved is null ? null : new AccountIdentity(saved.Username, saved.Uuid);
    }

    public AccountIdentity? ReadMuaIdentity()
    {
        var saved = mua?.Read();
        return saved is null ? null : new AccountIdentity(saved.Profile.Name, saved.Profile.Id);
    }

    public Task<MSession> AuthenticateMicrosoftAsync(bool interactive, CancellationToken token) =>
        microsoft?.Authenticate(interactive, token)
        ?? throw new AccountLoginException("登录组件未就绪。");

    public async Task<MSession> LoginMuaAsync(
        string account,
        string password,
        Func<IReadOnlyList<MuaProfile>, Task<MuaProfile?>> choose,
        CancellationToken token)
    {
        if (mua is null)
            throw new AccountLoginException("登录组件未就绪。");
        return MuaAccountService.Session(await mua.Login(account, password, choose, token));
    }

    public async Task<MSession> EnsureMuaAsync(bool interactive, CancellationToken token)
    {
        if (mua is null)
            throw new AccountLoginException("登录组件未就绪。");
        return MuaAccountService.Session(await mua.Ensure(interactive, token));
    }

    public void ForgetMicrosoft() => microsoft?.Forget();

    public Task ForgetMuaAsync(CancellationToken token) =>
        mua?.Forget(token) ?? Task.CompletedTask;
}
