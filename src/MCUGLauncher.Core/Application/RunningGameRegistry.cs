using System.Diagnostics;

namespace BlockLaunch;

/// <summary>
/// 表示 RunningGame 数据。
/// </summary>
public sealed record RunningGame(Guid Id, string Root, string Directory, string Version, string LogFile, GameSession Session, DateTime StartedUtc, string PlayerName = "", bool Offline = false);

/// <summary>Owns independent process lifetimes, not UI selection or account state.</summary>
public sealed class RunningGameRegistry
{
    readonly object gate = new();
    readonly List<RunningGame> games = [];

    public IReadOnlyList<RunningGame> Active
    {
        get
        {
            lock (gate)
                return games.Where(g => !g.Session.Completion.IsCompleted).ToArray();
        }
    }

    /// <summary>
    /// 执行 UsesDirectory。
    /// </summary>
    public bool UsesDirectory(string directory) => Active.Any(g => SamePath(g.Directory, directory));
    /// <summary>
    /// 执行 Uses根目录。
    /// </summary>
    public bool UsesRoot(string root) => Active.Any(g => SamePath(g.Root, root));

    /// <summary>
    /// 执行 RequireAvailable。
    /// </summary>
    public void RequireAvailable(string directory, string playerName = "", bool offline = false)
    {
        var conflicts = Active.Where(game => SamePath(game.Directory, directory));
        if (conflicts.Any(game => !offline || !game.Offline || string.IsNullOrWhiteSpace(playerName) ||
            game.PlayerName.Equals(playerName, StringComparison.OrdinalIgnoreCase)))
            throw new IOException("该实例已有相同身份或在线会话运行。离线多开请更换角色 ID，或选择另一个实例。");
    }

    /// <summary>
    /// 启动。
    /// </summary>
    public RunningGame Start(string root, string directory, string version, string logDirectory,
        Process process, Action<string> output, IEnumerable<string> secrets, string playerName = "", bool offline = false)
    {
        lock (gate)
        {
            RequireAvailable(directory, playerName, offline);
            var id = Guid.NewGuid();
            var log = Path.Combine(logDirectory, id.ToString("N") + ".log");
            var session = new GameSession(process, log, output, secrets);
            // Do not publish a session until Process.Start succeeds.
            session.Start();
            var game = new RunningGame(id, Path.GetFullPath(root), Path.GetFullPath(directory), version, log, session, DateTime.UtcNow, playerName, offline);
            games.Add(game);
            return game;
        }
    }

    /// <summary>
    /// 执行 Release。
    /// </summary>
    public void Release(Guid id)
    {
        lock (gate)
            games.RemoveAll(g => g.Id == id);
    }

    static bool SamePath(string left, string right) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)).Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)), StringComparison.OrdinalIgnoreCase);
}
