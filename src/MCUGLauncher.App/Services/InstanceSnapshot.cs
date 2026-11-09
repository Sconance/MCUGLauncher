using System.Text.Json;
using BlockLaunch;
using MCUGLauncher.Models;

namespace MCUGLauncher.Services;

/// <summary>冻结指定根目录下实例的身份、数据路径、需求与启动设置副本。</summary>
/// <remarks>B 类。依赖 AppConfig 的 Root/GameRoots/LegacyPreferences/AutoMemory/Ram/Java/Width/Height，
/// GameDirectoryCatalog.RequireRoot(AppConfig,string)、InstanceRequirements.Read(string,string)；
/// Core.InstanceCatalog.GetOrCreate/DirectoryFor 和 LaunchSettings 完成独立快照。
/// 不依赖队列、视图或当前页面。见 docs/components/GameDirectories.md。</remarks>
internal sealed record InstanceSnapshot(string Root, string Key, string Directory,
    string Name, PackRequirements Requirements, LaunchSettings Settings)
{
    public static InstanceSnapshot Capture(AppConfig config, string key, string? gameRoot = null)
    {
        var root = GameDirectoryCatalog.RequireRoot(config, gameRoot);
        ContentService.SafeFile(key);
        if (key is "." or ".." || !File.Exists(Path.Combine(root, "versions", key, key + ".json")))
            throw new IOException("目标实例不存在。");
        var catalog = new InstanceCatalog(root);
        var descriptor = catalog.GetOrCreate(key);
        var settings = SettingsFor(config, descriptor);
        return new(root, key, catalog.DirectoryFor(key), descriptor.DisplayName,
            InstanceRequirements.Read(root, key), settings);
    }

    public static LaunchSettings SettingsFor(AppConfig config, InstanceDescriptor descriptor)
    {
        var settings = JsonSerializer.Deserialize<LaunchSettings>(JsonSerializer.Serialize(
            descriptor.Launch ?? config.LegacyPreferences.Launch))!;
        if (descriptor.Launch == null)
        {
            settings.AutoMemory = config.AutoMemory;
            settings.RamMb = config.Ram;
            settings.Java = config.Java;
            settings.Width = config.Width;
            settings.Height = config.Height;
        }
        return settings;
    }

    public InstanceOperationRequest Request() => new()
    {
        Key = Key,
        Directory = Directory,
        Name = Name,
        Requirements = Requirements,
        Settings = Settings
    };
}
