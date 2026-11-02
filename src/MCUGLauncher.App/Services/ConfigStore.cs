using System.Text.Json;
using MCUGLauncher.Models;

namespace MCUGLauncher.Services;

/// <summary>
/// 启动器配置存储。
/// </summary>
/// <remarks>
/// 依赖 %LOCALAPPDATA%/MCUGLauncher/config.json；Load 读取当前配置并修正无效路径，Save 直接覆盖写入。
/// </remarks>
public sealed class ConfigStore
{
    readonly string _file;

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
    public void Save(AppConfig config)
    {
        config.LegacyPreferences.Root = config.Root;
        config.LegacyPreferences.Player = config.Player;
        config.LegacyPreferences.Java = config.Java;
        config.LegacyPreferences.Ram = config.Ram;
        config.LegacyPreferences.Width = config.Width;
        config.LegacyPreferences.Height = config.Height;
        config.LegacyPreferences.LastVersion = config.LastVersion;
        var temp = _file + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, _file, true);
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
