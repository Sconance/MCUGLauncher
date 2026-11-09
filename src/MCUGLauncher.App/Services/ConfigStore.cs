using System.Text.Json;
using BlockLaunch;
using MCUGLauncher.Models;

namespace MCUGLauncher.Services;

/// <summary>
/// 启动器配置存储。
/// </summary>
/// <remarks>
/// B 类。依赖 Models.AppConfig 的配置字段及 SavedServer.Instance/GameRoot；Load 导入旧配置，
/// 为旧收藏固定所属目录并保留离线磁盘路径，Save 使用现有原子提交机制写入。无 UI 依赖。
/// </remarks>
public sealed class ConfigStore
{
    const string MissingMarkdownAnnouncementsUrl =
        "https://raw.githubusercontent.com/waibibabu1239/MCUGL-storage/main/announcements.md";
    const string UnpublishedMainAnnouncementsUrl =
        "https://raw.githubusercontent.com/Sconance/MCUGLauncher/main/community/announcements.md";
    const string LegacyAvaloniaAnnouncementsUrl =
        "https://raw.githubusercontent.com/Sconance/MCUGLauncher/" +
        "0.22.3-Avalonia/community/announcements.md";
    readonly string _file;
    readonly bool dataDirectoryIsolated;
    readonly object writeGate = new();
    long nextWrite;
    long committedWrite;

    public ConfigStore(string? dataDirectory = null)
    {
        dataDirectoryIsolated = dataDirectory != null;
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
                config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(_file)) ?? new AppConfig();
            }
            catch (JsonException)
            {
                File.Move(_file, _file + ".broken-" + DateTime.Now.ToString("yyyyMMddHHmmss"), true);
                config = new AppConfig();
            }
        }
        else
        {
            config = dataDirectoryIsolated ? new AppConfig() : ImportLegacy() ?? new AppConfig();
        }

        config.DataSources ??= new DataSourceConfig();
        if (config.DataSourceMode != "custom"
            && (string.Equals(
                    config.DataSources.Announcements,
                    MissingMarkdownAnnouncementsUrl,
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    config.DataSources.Announcements,
                    UnpublishedMainAnnouncementsUrl,
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    config.DataSources.Announcements,
                    LegacyAvaloniaAnnouncementsUrl,
                    StringComparison.OrdinalIgnoreCase)))
        {
            config.DataSources.Announcements = new DataSourceConfig().Announcements;
        }

        // Disconnected disks must retain their saved library and selection.
        if (string.IsNullOrWhiteSpace(config.Root))
            config.Root = FindDefaultRoot();
        config.GameRoots ??= [];
        config.Servers ??= [];
        foreach (var server in config.Servers.Where(server => !string.IsNullOrWhiteSpace(server.Instance) && string.IsNullOrWhiteSpace(server.GameRoot)))
            server.GameRoot = config.Root;
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
    public void Save(AppConfig config)
    {
        Commit(Capture(config));
    }

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

    AppConfig? ImportLegacy()
    {
        var legacy = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BlockLaunch",
            "settings.json");
        return ReadLegacy(legacy);
    }

    internal static AppConfig? ReadLegacy(string legacy)
    {
        if (!File.Exists(legacy))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(legacy));
            var root = doc.RootElement;
            string Read(string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? ""
                : "";
            int ReadInt(string name, int fallback) => root.TryGetProperty(name, out var value) && value.TryGetInt32(out var number)
                ? number
                : fallback;

            var config = new AppConfig
            {
                LegacyPreferences = new BlockLaunch.SettingsStore(Path.GetDirectoryName(legacy)!).Load(),
                Root = Read("Root"),
                Player = Read("Player"),
                Java = Read("Java"),
                Ram = ReadInt("Ram", 4096),
                Width = ReadInt("Width", 1280),
                Height = ReadInt("Height", 720),
                LastVersion = Read("LastVersion"),
                SkinFile = Read("OfflineSkin")
            };

            if (root.TryGetProperty("Servers", out var servers) && servers.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in servers.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object)
                        continue;
                    var name = item.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "";
                    var address = item.TryGetProperty("Address", out var a) ? a.GetString() ?? "" : "";
                    if (address.Length > 0)
                        config.Servers.Add(new SavedServer { Name = name, Address = address });
                }
            }

            return config;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    static string FindDefaultRoot()
    {
        var roaming = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");
        if (Directory.Exists(roaming))
            return roaming;
        var local = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BlockLaunch",
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
