using System.Collections.Concurrent;
using BlockLaunch;
using CmlLib.Core.Auth;
using MCUGLauncher.Models;
using MCUGLauncher.Services;

namespace MCUGLauncher.UseCases;

internal sealed class LauncherContext : IDisposable
{
    readonly CancellationTokenSource lifetime = new();
    readonly ConcurrentDictionary<string, string> preparing = new(StringComparer.OrdinalIgnoreCase);
    static string Full(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    public void MarkPreparing(string root, string directory) => preparing[Full(directory)] = Full(root);
    public void ReleasePreparing(string directory) => preparing.TryRemove(Full(directory), out _);
    public bool IsInstanceInUse(string directory) => preparing.ContainsKey(Full(directory)) || Games.UsesDirectory(directory);
    public bool IsRootPreparing(string root) => preparing.Values.Any(value => value.Equals(Full(root), StringComparison.OrdinalIgnoreCase));
    public CancellationToken Token => lifetime.Token;
    public ConfigStore Store
    {
        get;
    }
    public CommunityService Community
    {
        get;
    }
    public HttpClient Http
    {
        get;
    }
    public MicrosoftAccountService? Microsoft
    {
        get; private set;
    }
    public MuaAccountService? Mua
    {
        get; private set;
    }
    public MSession? OnlineSession
    {
        get; set;
    }
    public AppConfig Config
    {
        get;
    }
    public Action<object> Send
    {
        get;
    }
    public RunningGameRegistry Games { get; } = new();
    public void SendGames() => Send(new { type = "games", items = Games.Active.Select(g => new { id = g.Id.ToString("N"), version = g.Version, player = g.PlayerName, pid = g.Session.Process.Id }) });

    public LauncherTaskQueue Tasks
    {
        get;
    }

    public void SendTasks() => Send(new { type = "tasks", items = Tasks.History().Take(200).Select(r => new { r.Id, r.Name, state = r.State.ToString(), r.Percent, r.Message, r.Committing }) });

    public Action RefreshState { get; set; } = () => { };

    public LauncherContext(Action<object> send, ConfigStore? store = null, Func<string, XboxAuthNet.OAuth.CodeFlow.IWebUI>? microsoftBrowser = null)
    {
        Send = send;
        Store = store ?? new ConfigStore();
        Config = Store.Load();
        Http = NetworkPolicyHandler.Create(Config.LegacyPreferences.Network);
        Community = new CommunityService(Http);
        var executor = new DownloadExecutor(Http, IsInstanceInUse, IsRootPreparing);
        Tasks = new LauncherTaskQueue(Path.Combine(Store.DataDirectory, "operations"),
            async (record, task) => { await executor.ExecuteAsync(record, task); RefreshState(); }, SendTasks, Log);
        var authDirectory = Path.Combine(Store.DataDirectory, "auth");
        try
        {
            Microsoft = new MicrosoftAccountService(authDirectory, Http, microsoftBrowser?.Invoke(authDirectory));
            Mua = new MuaAccountService(authDirectory, Http);
            if (Config.AccountType == "mua")
            {
                var saved = Mua.Read();
                if (saved != null)
                {
                    OnlineSession = MuaAccountService.Session(saved);
                    Config.Player = saved.Profile.Name;
                }
            }
        }
        catch
        {
            // A damaged account cache must never prevent the launcher from opening.
            Config.AccountType = "offline";
        }
    }

    public void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(Store.DataDirectory);
            File.AppendAllText(Path.Combine(Store.DataDirectory, "launcher.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }
    public void Dispose()
    {
        Tasks.Dispose();
        lifetime.Cancel();
        _ = ReleaseResourcesAsync();
    }
    async Task ReleaseResourcesAsync()
    {
        try
        {
            await Tasks.Completion;
        }
        finally { Http.Dispose(); lifetime.Dispose(); }
    }

}
