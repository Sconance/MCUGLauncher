namespace BlockLaunch;

/// <summary>
/// 提供 实例Descriptor 相关功能。
/// </summary>
public class InstanceDescriptor
{
    /// <summary>
    /// 获取或设置 Schema版本。
    /// </summary>
    public int SchemaVersion { get; set; } = 1;
    /// <summary>
    /// 获取或设置 实例ID。
    /// </summary>
    public string InstanceId { get; set; } = Guid.NewGuid().ToString("N");
    /// <summary>
    /// 获取或设置 Key。
    /// </summary>
    public string Key { get; set; } = "";
    /// <summary>
    /// 获取或设置 Display名称。
    /// </summary>
    public string DisplayName { get; set; } = "";
    /// <summary>
    /// 获取或设置 启动档案ID。
    /// </summary>
    public string LaunchProfileId { get; set; } = "";
    /// <summary>
    /// 获取或设置 Description。
    /// </summary>
    public string Description { get; set; } = "";
    /// <summary>
    /// 获取或设置 Category。
    /// </summary>
    public string Category { get; set; } = "自动";
    public bool Favorite
    {
        get;
        set;
    }
    /// <summary>
    /// 获取或设置 Icon路径。
    /// </summary>
    public string IconPath { get; set; } = "";
    /// <summary>
    /// 获取或设置 数据Directory。
    /// </summary>
    public string DataDirectory { get; set; } = "";
    public LaunchSettings? Launch
    {
        get;
        set;
    }
}

/// <summary>
/// 提供 实例Catalog 相关功能。
/// </summary>
public sealed class InstanceCatalog(string root)
{
    static string Safe(string id) => ContentService.SafeFile(id is "." or ".." ? throw new IOException("实例 ID 无效。") : id);
    /// <summary>
    /// 执行 MetadataDirectory。
    /// </summary>
    public string MetadataDirectory(string key) => Path.Combine(Path.GetFullPath(root), "instances", Safe(key));

    /// <summary>
    /// 执行 DirectoryFor。
    /// </summary>
    public string DirectoryFor(string key)
    {
        var file = Path.Combine(MetadataDirectory(key), ".blocklaunch-instance.json");
        var stored = File.Exists(file) ? AtomicFile.ReadJson<InstanceDescriptor>(file).DataDirectory : "";
        return stored.Length == 0 ? MetadataDirectory(key) : Path.GetFullPath(stored);
    }

    /// <summary>
    /// 保存。
    /// </summary>
    public void Save(InstanceDescriptor record)
    {
        GetOrCreate(record.Key);
        Safe(record.LaunchProfileId);
        AtomicFile.WriteJson(Path.Combine(MetadataDirectory(record.Key), ".blocklaunch-instance.json"), record);
    }

    /// <summary>
    /// 获取OrCreate。
    /// </summary>
    public InstanceDescriptor GetOrCreate(string key)
    {
        var directory = MetadataDirectory(key);
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, ".blocklaunch-instance.json");
        if (File.Exists(file))
        {
            var stored = AtomicFile.ReadJson<InstanceDescriptor>(file);
            if (stored.Key != key || stored.SchemaVersion != 1 || !Guid.TryParseExact(stored.InstanceId, "N", out _))
                throw new IOException("实例身份记录无效，已保留原数据。");
            Safe(stored.LaunchProfileId);
            return stored;
        }

        var record = new InstanceDescriptor
        {
            Key = key,
            DisplayName = key,
            LaunchProfileId = key
        };
        var legacyDirectory = Path.Combine(root, "versions", key);
        if (new[] {
            "mods",
            "saves",
            "config",
            "resourcepacks",
            "shaderpacks",
            "screenshots",
            "options.txt",
            "servers.dat"
        }.Any(d => Directory.Exists(Path.Combine(legacyDirectory, d)) || File.Exists(Path.Combine(legacyDirectory, d))))
            record.DataDirectory = Path.GetFullPath(legacyDirectory);
        var legacy = Path.Combine(directory, ".blocklaunch-pack.json");
        if (File.Exists(legacy))
        {
            var pack = AtomicFile.ReadJson<InstalledPack>(legacy);
            record.DisplayName = pack.Name;
            record.LaunchProfileId = Safe(pack.LaunchVersion);
        }

        AtomicFile.WriteJson(file, record);
        return record;
    }

    /// <summary>
    /// 执行 MigrateLegacy。
    /// </summary>
    public List<string> MigrateLegacy()
    {
        var errors = new List<string>();
        var directory = Path.Combine(root, "instances");
        if (!Directory.Exists(directory))
            return errors;
        foreach (var instance in Directory.GetDirectories(directory))
            try
            {
                GetOrCreate(Path.GetFileName(instance));
            }
            catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException)
            {
                errors.Add(Path.GetFileName(instance) + ": " + ex.Message);
            }

        return errors;
    }

    /// <summary>
    /// 创建。
    /// </summary>
    public string Create(string displayName, string launchProfile, string minecraft)
    {
        Safe(launchProfile);
        Safe(minecraft);
        var key = "instance-" + Guid.NewGuid().ToString("N")[..12];
        var directory = DirectoryFor(key);
        var record = new InstanceDescriptor
        {
            Key = key,
            DisplayName = displayName,
            LaunchProfileId = launchProfile
        };
        using var commit = OperationExecution.BeginCommit();
        Directory.CreateDirectory(directory);
        AtomicFile.WriteJson(Path.Combine(directory, ".blocklaunch-instance.json"), record);
        var profile = Path.Combine(root, "versions", key, key + ".json");
        AtomicFile.WriteJson(profile, new
        {
            id = key,
            inheritsFrom = launchProfile,
            jar = minecraft,
            type = "release"
        });
        return key;
    }
}

/// <summary>
/// 提供 版本Installation 相关功能。
/// </summary>
public sealed class VersionInstallation
{
    /// <summary>
    /// 获取或设置 版本。
    /// </summary>
    public string Version { get; set; } = "";
    public bool Ready
    {
        get;
        set;
    }
    /// <summary>
    /// 获取或设置 状态。
    /// </summary>
    public string State { get; set; } = "";
    public DateTimeOffset Updated
    {
        get;
        set;
    }

    static string FileFor(string root, string version) => Path.Combine(root, ".blocklaunch-installations", ContentService.SafeFile(version) + ".json");

    /// <summary>
    /// 判断是否为Ready。
    /// </summary>
    public static bool IsReady(string root, string version)
    {
        var file = FileFor(root, version);
        if (!File.Exists(file))
            return true;
        try
        {
            return AtomicFile.ReadJson<VersionInstallation>(file).Ready;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 执行 Run。
    /// </summary>
    public static async Task Run(string root, string version, Func<CancellationToken, Task> action, CancellationToken token)
    {
        var file = FileFor(root, version);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        using var lease = new FileStream(Path.Combine(Path.GetDirectoryName(file)!, "operation.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        AtomicFile.WriteJson(file, new VersionInstallation { Version = version, State = "Installing", Updated = DateTimeOffset.UtcNow });
        try
        {
            await action(token);
            token.ThrowIfCancellationRequested();
            AtomicFile.WriteJson(file, new VersionInstallation { Version = version, Ready = true, State = "Ready", Updated = DateTimeOffset.UtcNow });
        }
        catch (Exception ex)
        {
            AtomicFile.WriteJson(file, new VersionInstallation { Version = version, State = ex is OperationCanceledException ? "Cancelled" : "Incomplete", Updated = DateTimeOffset.UtcNow });
            throw;
        }
    }
}
