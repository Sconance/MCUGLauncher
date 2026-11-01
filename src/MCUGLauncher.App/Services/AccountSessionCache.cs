using CmlLib.Core.Auth;

namespace MCUGLauncher.Services;

// Process-local verified sessions. Selecting an account does not renew its verification age.
internal sealed class AccountSessionCache(Func<DateTimeOffset>? clock = null)
{
    static readonly TimeSpan Freshness = TimeSpan.FromMinutes(5);
    readonly object gate = new();
    readonly Dictionary<string, (MSession Session, DateTimeOffset Verified)> sessions = new();
    DateTimeOffset Now => clock?.Invoke() ?? DateTimeOffset.UtcNow;

    public void Remember(string mode, MSession session)
    {
        lock (gate)
            sessions[mode] = (session, Now);
    }

    public MSession? Find(string mode)
    {
        lock (gate)
            return sessions.TryGetValue(mode, out var value) ? value.Session : null;
    }

    public MSession? Fresh(string mode)
    {
        lock (gate)
        {
            if (!sessions.TryGetValue(mode, out var value))
                return null;
            var age = Now - value.Verified;
            return age >= TimeSpan.Zero && age < Freshness ? value.Session : null;
        }
    }

    public void Forget(string mode)
    {
        lock (gate)
            sessions.Remove(mode);
    }
}
