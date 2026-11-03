using System.Text.Json;
using MCUGLauncher.Models;
using MCUGLauncher;

namespace MCUGLauncher.Services;

/// <summary>
/// 启动器配置存储。
/// </summary>
/// <remarks>
/// B 类。依赖 AppConfig.Root/GameRoots/Player/OfflinePlayer/AccountType/LastVersion/LastInstance/Java/Ram/Width/Height/LegacyPreferences；
/// 读取并补全配置，同步 Core Preferences.Root/Player/Java/Ram/Width/Height/LastVersion。
/// Core AtomicFile.ReadJson&lt;AppConfig&gt;(string)/WriteJson&lt;AppConfig&gt;(string,AppConfig) 负责原子存储。
/// 默认使用 %LOCALAPPDATA%/MCUGLauncher/config.json，不依赖窗口、视图、Commands 或其他服务。
/// 依赖详情见 docs/components/ConfigStore.md。
/// </remarks>
public sealed class ConfigStore
{
    readonly string _file;
    readonly object writeGate = new();
    long nextWrite, committedWrite;

    public ConfigStore(string? dataDirectory = null)
    {
        DataDirectory = dataDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MCUGLauncher");
        System.IO.Directory.CreateDirectory(DataDirectory);
        _file = Path.Combine(DataDirectory, "config.json");
    }

    public string DataDirectory
    {
        get;
    }

    /// <summary>
    /// 读取配置。
    /// </summary>
    /// <returns>补全默认值后的配置对象</returns>
    /// <remarks>
    /// JSON 损坏时把原文件改名为 .broken-时间戳 后返回默认值；Root/Player/LastVersion 缺失时自动补全。
    /// </remarks>
    public AppConfig Load()
    {
        AppConfig config;
        if (File.Exists(_file))
        {
            try
            {
                config = AtomicFile.ReadJson<AppConfig>(_file);
            }
            catch (Exception error) when (error is JsonException or InvalidDataException)
            {
                File.Move(_file, _file + ".broken-" + DateTime.Now.ToString("yyyyMMddHHmmss"), true);
                config = new AppConfig();
            }
        }
        else
        {
            config = new AppConfig();
        }

        if (string.IsNullOrWhiteSpace(config.Root) || !Directory.Exists(config.Root))
            config.Root = FindDefaultRoot();
        config.GameRoots ??= [];
        if (string.IsNullOrWhiteSpace(config.Player))
            config.Player = "Steve";
        if (string.IsNullOrWhiteSpace(config.OfflinePlayer))
            config.OfflinePlayer = config.AccountType == "offline" ? config.Player : "Steve";
        if (string.IsNullOrWhiteSpace(config.LastVersion))
            config.LastVersion = FindFirstVersion(config.Root);
        if (string.IsNullOrWhiteSpace(config.LastInstance))
            config.LastInstance = config.LastVersion;
        return config;
    }

    /// <summary>
    /// 保存配置。
    /// </summary>
    /// <param name="config">要写入的配置对象</param>
    /// <remarks>
    /// 不校验字段，调用方负责保证 Root 等路径有效。
    /// </remarks>
    public void Save(AppConfig config) => Commit(Capture(config));

    /// <summary>在调用线程捕获独立快照，后台原子写入；旧快照不得覆盖较新的同步或异步保存。</summary>
    public Task SaveAsync(AppConfig config)
    {
        var snapshot = Capture(config);
        return Task.Run(() => Commit(snapshot));
    }
    (long Revision, JsonElement Data) Capture(AppConfig config)
    {
        config.LegacyPreferences.Root = config.Root;
        config.LegacyPreferences.Player = config.Player;
        config.LegacyPreferences.Java = config.Java;
        config.LegacyPreferences.Ram = config.Ram;
        config.LegacyPreferences.Width = config.Width;
        config.LegacyPreferences.Height = config.Height;
        config.LegacyPreferences.LastVersion = config.LastVersion;
        return (Interlocked.Increment(ref nextWrite), JsonSerializer.SerializeToElement(config));
    }
    void Commit((long Revision, JsonElement Data) snapshot)
    {
        lock (writeGate)
        {
            if (snapshot.Revision < committedWrite)
                return;
            AtomicFile.WriteJson(_file, snapshot.Data);
            committedWrite = snapshot.Revision;
        }
    }

    static string FindDefaultRoot()
    {
        var roaming = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");
        if (Directory.Exists(roaming))
            return roaming;
        var local = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MCUGLauncher",
            "minecraft");
        Directory.CreateDirectory(local);
        return local;
    }

    static string FindFirstVersion(string root)
    {
        var versions = Path.Combine(root, "versions");
        if (!Directory.Exists(versions))
            return "";
        return Directory.EnumerateDirectories(versions)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .OrderByDescending(name => name)
            .FirstOrDefault() ?? "";
    }
}
