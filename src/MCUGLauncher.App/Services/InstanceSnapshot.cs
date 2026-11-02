using System.Text.Json;
using MCUGLauncher;
using MCUGLauncher.Models;

namespace MCUGLauncher.Services;

internal sealed record InstanceSnapshot(string Root, string Key, string Directory,
    string Name, PackRequirements Requirements, LaunchSettings Settings)
{
    public static InstanceSnapshot Capture(AppConfig config, string key)
    {
        var root = Path.GetFullPath(config.Root);
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
