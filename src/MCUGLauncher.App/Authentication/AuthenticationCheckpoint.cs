using XboxAuthNet.Game.Authenticators;

namespace BlockLaunch;

// Logs a fixed phase label only. No request URL, account name or token crosses this boundary.
internal sealed class AuthenticationCheckpoint(Action reached) : IAuthenticator
{
    public ValueTask ExecuteAsync(AuthenticateContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        reached();
        return ValueTask.CompletedTask;
    }
}
