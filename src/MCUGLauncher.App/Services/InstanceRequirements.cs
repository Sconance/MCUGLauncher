using MCUGLauncher;

namespace MCUGLauncher.Services;

/// <summary>优先读取整合包安装记录，其余版本复用 Core 元数据解析。</summary>
/// <remarks>A 类，仅依赖 Core/BCL；不从显示名称猜加载器，不写入文件。</remarks>
internal static class InstanceRequirements
{
    public static PackRequirements Read(string root, string key)
    {
        ContentService.SafeFile(key);
        var pack = Path.Combine(root, "instances", key, ".mcuglauncher-pack.json");
        if (File.Exists(pack))
        {
            var installed = AtomicFile.ReadJson<InstalledPack>(pack);
            return new(installed.Minecraft, installed.Loader, installed.LoaderVersion);
        }
        return ProfileInspector.Read(root, key);
    }
}
