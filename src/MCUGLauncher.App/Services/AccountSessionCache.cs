using CmlLib.Core.Auth;

namespace MCUGLauncher.Services;

/// <summary>本地身份摘要不含令牌，与已验证会话分开缓存。</summary>
internal sealed record AccountIdentity(string Player, string Uuid);

// Selecting a local identity never marks its authorization as verified.
internal sealed class AccountSessionCache(Func<DateTimeOffset>? clock = null)
{
    static readonly TimeSpan Freshness = TimeSpan.FromMinutes(5);
    readonly object gate = new();
    readonly Dictionary<string, (MSession Session, DateTimeOffset Verified)> sessions = new();
    readonly Dictionary<string, AccountIdentity> identities = new();
    DateTimeOffset Now => clock?.Invoke() ?? DateTimeOffset.UtcNow;

    public void Remember(string mode, MSession session)
    {
        lock (gate)
        {
            sessions[mode] = (session, Now);
            identities[mode] = new(session.Username ?? "", session.UUID ?? "");
        }
    }

    public AccountIdentity? Identity(string mode)
    {
        lock (gate)
            return identities.GetValueOrDefault(mode);
    }
    public void RememberIdentity(string mode, AccountIdentity identity)
    {
        if (string.IsNullOrWhiteSpace(identity.Player))
            throw new ArgumentException("账号缺少玩家名");
        lock (gate)
            identities[mode] = identity;
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
        {
            sessions.Remove(mode);
            identities.Remove(mode);
        }
    }
}
