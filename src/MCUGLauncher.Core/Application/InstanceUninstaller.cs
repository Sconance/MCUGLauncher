using System.Text.Json;

namespace BlockLaunch;

/// <summary>
/// 表示 UninstallPlan 数据。
/// </summary>
public sealed record UninstallPlan(string Root, string Key, string InstanceId, string Name, bool SharedData, bool DeleteSharedData, List<string> Paths);

/// <summary>
/// 提供 实例Uninstaller 相关功能。
/// </summary>
public static class InstanceUninstaller
{
    static readonly string[] SharedItems = ["saves", "mods", "config", "defaultconfigs", "resourcepacks", "shaderpacks", "screenshots", "logs", "crash-reports", "options.txt", "optionsof.txt", "servers.dat", "servers.dat_old"];

    static string Full(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    static bool Same(string a, string b) => Full(a).Equals(Full(b), StringComparison.OrdinalIgnoreCase);
    /// <summary>
    /// 执行 Inside。
    /// </summary>
    public static bool Inside(string path, string parent) => Same(path, parent) || Full(path).StartsWith(Path.EndsInDirectorySeparator(Full(parent)) ? Full(parent) : Full(parent) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    static void NoLinks(string path, bool recursive = true)
    {
        for (var current = Full(path); current != null; current = Path.GetDirectoryName(current))
            if ((Directory.Exists(current) || File.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("删除范围包含链接或联接，已停止：" + current);
        var pending = new Stack<string>();
        if (recursive && Directory.Exists(path))
            pending.Push(path);
        while (pending.TryPop(out var directory))
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("删除范围包含链接或联接，已停止：" + entry);
                if ((attributes & FileAttributes.Directory) != 0)
                    pending.Push(entry);
            }
    }

    /// <summary>
    /// 执行 Plan。
    /// </summary>
    public static UninstallPlan Plan(string root, string key, LaunchSettings defaults, bool deleteSharedData = false)
    {
        root = Full(root);
        ContentService.SafeFile(key);
        if (key is "." or "..")
            throw new IOException("版本标识无效。");
        NoLinks(root, false);
        var catalog = new InstanceCatalog(root);
        var descriptor = catalog.GetOrCreate(key);
        var profile = Path.Combine(root, "versions", key);
        var metadata = catalog.MetadataDirectory(key);
        var data = catalog.DirectoryFor(key);
        var shared = (descriptor.Launch ?? defaults).Isolation == "共享根目录" || Same(data, root);
        if (!shared && !Same(data, profile) && !Same(data, metadata))
            throw new IOException("此版本使用外部数据目录，请先手动核对后清理：" + data);
        var versions = Path.Combine(root, "versions");
        if (Directory.Exists(versions))
            foreach (var directory in Directory.GetDirectories(versions))
            {
                var other = Path.GetFileName(directory);
                if (other.Equals(key, StringComparison.OrdinalIgnoreCase))
                    continue;
                var jsonFile = Path.Combine(directory, other + ".json");
                if (!File.Exists(jsonFile))
                    continue;
                using var json = JsonDocument.Parse(File.ReadAllText(jsonFile));
                foreach (var field in new[] {
                    "inheritsFrom",
                    "jar"
                }

                )
                    if (json.RootElement.TryGetProperty(field, out var value) && value.GetString()?.Equals(key, StringComparison.OrdinalIgnoreCase) == true)
                        throw new IOException("版本「" + other + "」依赖此版本，请先卸载依赖版本。");
            }

        var paths = new List<string>
        {
            profile,
            metadata
        };
        if (shared && deleteSharedData)
            paths.AddRange(SharedItems.Select(name => Path.Combine(root, name)));
        // Another instance may explicitly point into the selected instance's private directory.
        var instances = Path.Combine(root, "instances");
        if (Directory.Exists(instances))
            foreach (var directory in Directory.GetDirectories(instances))
            {
                var other = Path.GetFileName(directory);
                if (other.Equals(key, StringComparison.OrdinalIgnoreCase))
                    continue;
                var file = Path.Combine(directory, ".blocklaunch-instance.json");
                if (!File.Exists(file))
                    continue;
                var otherDescriptor = AtomicFile.ReadJson<InstanceDescriptor>(file);
                var otherData = otherDescriptor.DataDirectory.Length > 0 ? Full(otherDescriptor.DataDirectory) : Full(directory);
                if (Inside(otherData, metadata) || Inside(otherData, profile))
                    throw new IOException("其他实例正在共用此版本的数据目录：" + other);
            }

        var marker = Path.Combine(root, ".blocklaunch-installations", key + ".json");
        paths.Add(marker);
        paths = paths.Select(Full).Distinct(StringComparer.OrdinalIgnoreCase).Where(p => File.Exists(p) || Directory.Exists(p)).ToList();
        foreach (var path in paths)
        {
            if (!Inside(path, root) || Same(path, root))
                throw new IOException("卸载路径不在允许范围内。");
            NoLinks(path);
        }

        return new(root, key, descriptor.InstanceId, descriptor.DisplayName, shared, shared && deleteSharedData, paths);
    }

    /// <summary>
    /// 删除。
    /// </summary>
    public static void Delete(UninstallPlan confirmed, LaunchSettings defaults)
    {
        var current = Plan(confirmed.Root, confirmed.Key, defaults, confirmed.DeleteSharedData);
        if (current.InstanceId != confirmed.InstanceId || current.SharedData != confirmed.SharedData || !current.Paths.SequenceEqual(confirmed.Paths, StringComparer.OrdinalIgnoreCase))
            throw new IOException("实例身份或删除范围已变化，请重新确认卸载。");
        // Keep all targets in a journal so a locked file cannot leave untraceable remnants.
        var journal = Path.Combine(confirmed.Root, ".blocklaunch-uninstalls", Guid.NewGuid().ToString("N") + ".json");
        AtomicFile.WriteJson(journal, confirmed);
        using var commit = OperationExecution.BeginCommit();
        try
        {
            foreach (var path in confirmed.Paths.OrderBy(p => Same(p, Path.Combine(confirmed.Root, "versions", confirmed.Key)) ? 1 : 0))
            {
                NoLinks(path);
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
                else if (File.Exists(path))
                    File.Delete(path);
            }

            File.Delete(journal);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException("卸载未完全完成，部分文件可能被占用。请关闭游戏后重试。删除范围记录保存在：" + journal, ex);
        }
    }
}
