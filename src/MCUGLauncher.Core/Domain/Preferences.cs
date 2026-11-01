namespace BlockLaunch;

/// <summary>
/// 提供 Preferences 相关功能。
/// </summary>
public class Preferences
{
    /// <summary>
    /// 获取或设置 Schema版本。
    /// </summary>
    public int SchemaVersion { get; set; } = 1;
    /// <summary>
    /// 获取或设置 根目录。
    /// </summary>
    public string Root { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BlockLaunch", "minecraft");
    /// <summary>
    /// 获取或设置 Java。
    /// </summary>
    public string Java { get; set; } = "";
    /// <summary>
    /// 获取或设置 Player。
    /// </summary>
    public string Player { get; set; } = "Player";
    /// <summary>
    /// 获取或设置 Ram。
    /// </summary>
    public int Ram { get; set; } = 4096;
    /// <summary>
    /// 获取或设置 Width。
    /// </summary>
    public int Width { get; set; } = 1280;
    /// <summary>
    /// 获取或设置 Height。
    /// </summary>
    public int Height { get; set; } = 720;
    /// <summary>
    /// 获取或设置 Last版本。
    /// </summary>
    public string LastVersion { get; set; } = "";
    /// <summary>
    /// 获取或设置 GameRoots。
    /// </summary>
    public List<string> GameRoots { get; set; } = [];
    /// <summary>
    /// 获取或设置 启动。
    /// </summary>
    public LaunchSettings Launch { get; set; } = new();
    /// <summary>
    /// 获取或设置 Accent。
    /// </summary>
    public string Accent { get; set; } = "蓝色";
    /// <summary>
    /// 获取或设置 Background路径。
    /// </summary>
    public string BackgroundPath { get; set; } = "";
    /// <summary>
    /// 获取或设置 BackgroundOpacity。
    /// </summary>
    public double BackgroundOpacity { get; set; } = 0.22;
    /// <summary>
    /// 获取或设置 BackgroundFit。
    /// </summary>
    public string BackgroundFit { get; set; } = "裁剪填充";
    /// <summary>
    /// 获取或设置 BackgroundImages。
    /// </summary>
    public List<string> BackgroundImages { get; set; } = [];
    /// <summary>
    /// 获取或设置 Offline皮肤。
    /// </summary>
    public string OfflineSkin { get; set; } = "";
    public bool PreferMicrosoft
    {
        get;
        set;
    }
    public bool PreferMua
    {
        get;
        set;
    }
    /// <summary>
    /// 获取或设置 Network。
    /// </summary>
    public NetworkSettings Network { get; set; } = new();
    /// <summary>
    /// 获取或设置 Resource下载Directory。
    /// </summary>
    public string ResourceDownloadDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "BlockLaunch");
    public bool DownloadsPaused
    {
        get;
        set;
    }
    /// <summary>
    /// 获取或设置 Paused下载ID。
    /// </summary>
    public string PausedDownloadId { get; set; } = "";
    /// <summary>
    /// 获取或设置 ProtectSaves。
    /// </summary>
    public bool ProtectSaves { get; set; } = true;
    /// <summary>
    /// 获取或设置 Save备份Count。
    /// </summary>
    public int SaveBackupCount { get; set; } = 5;
    public bool DarkMode
    {
        get;
        set;
    }
    /// <summary>
    /// 获取或设置 ExtraJavaPaths。
    /// </summary>
    public List<string> ExtraJavaPaths { get; set; } = [];
    /// <summary>
    /// 获取或设置 Servers。
    /// </summary>
    public List<ServerFavorite> Servers { get; set; } = [];
}

/// <summary>
/// 提供 设置Store 相关功能。
/// </summary>
public sealed class SettingsStore(string directory)
{
    public string? Notice
    {
        get;
        private set;
    }

    /// <summary>
    /// 加载。
    /// </summary>
    public Preferences Load()
    {
        var file = Path.Combine(directory, "settings.json");
        if (!File.Exists(file))
            return new()
            {
                SchemaVersion = 2
            };
        try
        {
            var prefs = AtomicFile.ReadJson<Preferences>(file);
            using (var original = System.Text.Json.JsonDocument.Parse(File.ReadAllText(file)))
                if (!original.RootElement.TryGetProperty("Launch", out _))
                    prefs.Launch = new LaunchSettings
                    {
                        AutoMemory = false,
                        RamMb = prefs.Ram,
                        Java = prefs.Java,
                        Width = prefs.Width,
                        Height = prefs.Height
                    };
            if (prefs.SchemaVersion > 2)
                throw new NotSupportedException("配置来自更高版本，已保留原文件，请使用对应启动器。");
            if (string.IsNullOrWhiteSpace(prefs.Root) || prefs.Ram < 512 || prefs.Width < 320 || prefs.Height < 240)
                throw new InvalidDataException("配置值无效。");
            prefs.Root = Path.GetFullPath(prefs.Root);
            if (prefs.SchemaVersion < 2)
            {
                var backup = file + ".v1.backup";
                if (!File.Exists(backup))
                    File.Copy(file, backup);
                prefs.SchemaVersion = 2;
                Save(prefs);
                Notice = "配置已迁移至 v2，原配置保留为 settings.json.v1.backup。";
            }

            return prefs;
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidDataException or ArgumentException)
        {
            var backup = file + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
            File.Move(file, backup);
            var prefs = new Preferences
            {
                SchemaVersion = 2
            };
            Save(prefs);
            Notice = "损坏的配置已单独备份，已加载默认设置。";
            return prefs;
        }
    }

    /// <summary>
    /// 保存。
    /// </summary>
    public void Save(Preferences prefs)
    {
        var file = Path.Combine(directory, "settings.json");
        if (File.Exists(file))
        {
            try
            {
                if (AtomicFile.ReadJson<Preferences>(file).SchemaVersion > 2)
                    throw new NotSupportedException("不能覆盖更高版本的配置。");
            }
            catch (System.Text.Json.JsonException)
            {
                throw new InvalidDataException("原配置损坏，请先重新打开启动器恢复配置。");
            }
        }

        prefs.SchemaVersion = 2;
        AtomicFile.WriteJson(file, prefs);
    }
}
