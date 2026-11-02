using MCUGLauncher;
using MCUGLauncher.Models;

namespace MCUGLauncher.Services;

/// <summary>冻结实例目标与启动选项，后续选择变化不改变执行对象。</summary>
/// <remarks>
/// B 类。依赖 AppConfig.Root/LegacyPreferences.Launch/AutoMemory/Ram/Java/Width/Height 读取全局默认；
/// InstanceRequirements.Read(string,string) 提供要求；Core InstanceCatalog.GetOrCreate/DirectoryFor 提供实例身份与目录。
/// 只返回副本，不修改 AppConfig，不依赖任务、账号、窗口或视图。详见 docs/components/InstanceSnapshot.md。
/// </remarks>
internal sealed record InstanceSnapshot(string Root, string Key, string Directory,
    string Name, PackRequirements Requirements, LaunchSettings Settings)
{
    public static InstanceSnapshot Capture(AppConfig config, string key) =>
        Capture(config.Root, key, GlobalSettings(config));

    // Explicit input keeps a preview bound to its original root, without cloning account/config state.
    public static InstanceSnapshot Capture(string gameRoot, string key, LaunchSettings defaults)
    {
        var root = Path.GetFullPath(gameRoot);
        ContentService.SafeFile(key);
        if (key is "." or ".." || !File.Exists(Path.Combine(root, "versions", key, key + ".json")))
            throw new IOException("目标实例不存在。");
        var catalog = new InstanceCatalog(root);
        var descriptor = catalog.GetOrCreate(key);
        var settings = (descriptor.Launch ?? defaults).Copy();
        return new(root, key, catalog.DirectoryFor(key), descriptor.DisplayName,
            InstanceRequirements.Read(root, key), settings);
    }

    public static LaunchSettings GlobalSettings(AppConfig config)
    {
        var settings = config.LegacyPreferences.Launch.Copy();
        settings.AutoMemory = config.AutoMemory;
        settings.RamMb = config.Ram;
        settings.Java = config.Java;
        settings.Width = config.Width;
        settings.Height = config.Height;
        return settings;
    }

    public static LaunchSettings SettingsFor(AppConfig config, InstanceDescriptor descriptor) =>
        descriptor.Launch?.Copy() ?? GlobalSettings(config);

    public InstanceOperationRequest Request() => new()
    {
        Key = Key,
        Directory = Directory,
        Name = Name,
        Requirements = Requirements,
        Settings = Settings
    };
}
