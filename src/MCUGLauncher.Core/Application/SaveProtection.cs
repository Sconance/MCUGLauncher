using System.Security.Cryptography;
using System.Text;

namespace BlockLaunch;

/// <summary>
/// 表示 Save备份项目 数据。
/// </summary>
public record SaveBackupItem(string World, string Archive, string Version);

/// <summary>
/// 表示 Save备份 数据。
/// </summary>
public record SaveBackup(string Id, string Instance, string Reason, DateTimeOffset Created, List<SaveBackupItem> Worlds);

/// <summary>
/// 提供 SaveProtection 相关功能。
/// </summary>
public static class SaveProtection
{
    static void Safe(string path)
    {
        for (var item = new DirectoryInfo(Path.GetFullPath(path)); item != null; item = item.Parent)
            if (item.Exists && (item.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("备份路径不可包含链接。");
    }

    /// <summary>
    /// 执行 DirectoryFor。
    /// </summary>
    public static string DirectoryFor(string root, string instance) => Path.Combine(Path.GetFullPath(root), Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(instance).ToUpperInvariant())))[..24]);

    /// <summary>
    /// 执行 列表。
    /// </summary>
    public static List<SaveBackup> List(string root, string instance)
    {
        var directory = DirectoryFor(root, instance);
        Safe(directory);
        if (!Directory.Exists(directory))
            return [];
        var results = new List<SaveBackup>();
        foreach (var folder in Directory.EnumerateDirectories(directory))
            try
            {
                var backup = Read(folder);
                if (Path.GetFullPath(backup.Instance).Equals(Path.GetFullPath(instance), StringComparison.OrdinalIgnoreCase))
                    results.Add(backup);
            }
            catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or ArgumentException)
            {
            }

        return results.OrderByDescending(b => b.Created).ToList();
    }

    static SaveBackup Read(string folder)
    {
        Safe(folder);
        var manifest = Path.Combine(folder, "manifest.json");
        if (File.Exists(manifest) && (File.GetAttributes(manifest) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("不读取链接清单。");
        var backup = AtomicFile.ReadJson<SaveBackup>(manifest);
        if (!Guid.TryParseExact(backup.Id, "N", out _) || Path.GetFileName(folder) != backup.Id || backup.Worlds.Count > 10000)
            throw new IOException("备份清单无效。");
        foreach (var item in backup.Worlds)
            if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(item.Archive), "N", out _) || Path.GetExtension(item.Archive) != ".zip")
                throw new IOException("备份文件名无效。");
        return backup;
    }

    /// <summary>
    /// 创建。
    /// </summary>
    public static async Task<SaveBackup?> Create(string instance, string root, string reason, int keep, CancellationToken token = default)
    {
        if (keep is < 1 or > 100)
            throw new IOException("备份保留数量应为 1–100。");
        instance = Path.GetFullPath(instance);
        var directory = DirectoryFor(root, instance);
        Safe(instance);
        Safe(directory);
        if (InstanceUninstaller.Inside(directory, instance) || directory.Equals(instance, StringComparison.OrdinalIgnoreCase))
            throw new IOException("备份目录必须位于实例之外。");
        var worlds = WorldArchives.List(instance);
        if (worlds.Count == 0)
            return null;
        Directory.CreateDirectory(directory);
        var id = Guid.NewGuid().ToString("N");
        var folder = Path.Combine(directory, id);
        Directory.CreateDirectory(folder);
        var items = new List<SaveBackupItem>();
        var backup = new SaveBackup(id, instance, reason, DateTimeOffset.UtcNow, items);
        try
        {
            foreach (var world in worlds)
            {
                token.ThrowIfCancellationRequested();
                var archive = Guid.NewGuid().ToString("N") + ".zip";
                var version = "未知";
                try
                {
                    version = WorldMetadata.Read(Path.Combine(world.Directory, "level.dat")).Version;
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException)
                {
                }

                await WorldArchives.Backup(world.Directory, Path.Combine(folder, archive), token: token);
                items.Add(new(world.Name, archive, version));
            }

            token.ThrowIfCancellationRequested();
            AtomicFile.WriteJson(Path.Combine(folder, "manifest.json"), backup);
        }
        catch
        {
            foreach (var file in Directory.GetFiles(folder))
                File.Delete(file);
            Directory.Delete(folder);
            throw;
        }
        // Only rotate complete launcher-owned snapshots with exactly the expected files.
        foreach (var old in List(root, instance).Skip(keep))
        {
            token.ThrowIfCancellationRequested();
            var oldFolder = Path.Combine(directory, old.Id);
            Safe(oldFolder);
            var expected = old.Worlds.Select(w => w.Archive).Append("manifest.json").ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (Directory.GetDirectories(oldFolder).Length != 0 || !Directory.GetFiles(oldFolder).Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(expected))
                continue;
            foreach (var file in Directory.GetFiles(oldFolder))
            {
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("备份包含链接，停止轮换。");
            }

            foreach (var file in Directory.GetFiles(oldFolder))
                File.Delete(file);
            Directory.Delete(oldFolder);
        }

        return backup;
    }

    /// <summary>
    /// 执行 Archive路径。
    /// </summary>
    public static string ArchivePath(string root, string instance, SaveBackup backup, SaveBackupItem item)
    {
        if (!Guid.TryParseExact(backup.Id, "N", out _))
            throw new IOException("备份标识无效。");
        var folder = Path.Combine(DirectoryFor(root, instance), backup.Id);
        var verified = Read(folder);
        if (!verified.Worlds.Contains(item) || !verified.Instance.Equals(instance, StringComparison.OrdinalIgnoreCase))
            throw new IOException("备份条目不匹配。");
        var file = Path.Combine(folder, item.Archive);
        if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("不读取链接备份。");
        return file;
    }
}
