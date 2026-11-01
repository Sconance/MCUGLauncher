using System.IO.Compression;

namespace BlockLaunch;

/// <summary>
/// 表示 WorldEntry 数据。
/// </summary>
public record WorldEntry(string Name, string Directory, DateTime Modified)
{
    /// <summary>
    /// 执行 ToString。
    /// </summary>
    public override string ToString() => $"{Name}   ·   {Modified:yyyy-MM-dd HH:mm}";
}

/// <summary>
/// 表示 ScreenshotEntry 数据。
/// </summary>
public record ScreenshotEntry(string Name, string File, DateTime Modified, long Bytes)
{
    /// <summary>
    /// 执行 ToString。
    /// </summary>
    public override string ToString() => $"{Name}   ·   {Bytes / 1024:N0} KB";
}

/// <summary>World archives are staged and never overwrite an existing save.</summary>
public static class WorldArchives
{
    const long MaxBytes = 8L * 1024 * 1024 * 1024;
    const int MaxEntries = 100_000;

    static void NoLink(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("暂不处理链接或联接目录：" + Path.GetFileName(path));
    }

    static string World(string world)
    {
        world = Path.GetFullPath(world);
        NoLink(world);
        var level = Path.Combine(world, "level.dat");
        if (!File.Exists(level))
            throw new IOException("不是有效的存档目录（缺少 level.dat）。");
        NoLink(level);
        return world;
    }

    /// <summary>
    /// 执行 列表。
    /// </summary>
    public static List<WorldEntry> List(string instance)
    {
        var saves = Path.Combine(instance, "saves");
        if (!Directory.Exists(saves))
            return [];
        NoLink(saves);
        var result = new List<WorldEntry>();
        foreach (var directory in Directory.EnumerateDirectories(saves))
        {
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                continue;
            var level = Path.Combine(directory, "level.dat");
            if (File.Exists(level) && (File.GetAttributes(level) & FileAttributes.ReparsePoint) == 0)
                result.Add(new(Path.GetFileName(directory), Path.GetFullPath(directory), File.GetLastWriteTime(level)));
        }

        return result.OrderByDescending(w => w.Modified).ToList();
    }

    /// <summary>
    /// 执行 Screenshots。
    /// </summary>
    public static List<ScreenshotEntry> Screenshots(string instance)
    {
        var directory = Path.Combine(instance, "screenshots");
        if (!Directory.Exists(directory))
            return [];
        NoLink(directory);
        return Directory.EnumerateFiles(directory).Where(f => Path.GetExtension(f).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg").Where(f => (File.GetAttributes(f) & FileAttributes.ReparsePoint) == 0).Select(f => new FileInfo(f)).Select(f => new ScreenshotEntry(f.Name, f.FullName, f.LastWriteTime, f.Length)).OrderByDescending(f => f.Modified).ToList();
    }

    static FileStream HoldWorld(string world)
    {
        var session = Path.Combine(world, "session.lock");
        if (File.Exists(session))
            NoLink(session);
        try
        {
            return new FileStream(session, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException ex)
        {
            throw new IOException("存档正在使用或无法锁定，请退出使用此存档的游戏后再备份。", ex);
        }
    }

    static IEnumerable<string> Files(string directory, CancellationToken token)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
        {
            token.ThrowIfCancellationRequested();
            NoLink(entry);
            if (Directory.Exists(entry))
            {
                foreach (var file in Files(entry, token))
                    yield return file;
            }
            else
                yield return entry;
        }
    }

    /// <summary>
    /// 备份。
    /// </summary>
    public static async Task Backup(string world, string target, Action<string>? progress = null, CancellationToken token = default)
    {
        world = World(world);
        target = Path.GetFullPath(target);
        if (target.StartsWith(world.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new IOException("备份 ZIP 请保存到存档目录之外。");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        using var worldLock = HoldWorld(world);
        var temp = Path.Combine(Path.GetDirectoryName(target)!, ".blocklaunch-world-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var zip = new ZipArchive(new FileStream(temp, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None), ZipArchiveMode.Create))
            {
                long total = 0;
                int count = 0;
                foreach (var file in Files(world, token))
                {
                    var relative = Path.GetRelativePath(world, file).Replace('\\', '/');
                    if (relative == "session.lock")
                        continue;
                    PackService.SafePath(world, relative);
                    var length = new FileInfo(file).Length;
                    total = checked(total + length);
                    if (++count > MaxEntries || total > MaxBytes)
                        throw new IOException("存档备份超过限制（8 GiB / 10 万文件）。");
                    progress?.Invoke("备份存档：" + relative);
                    var entry = zip.CreateEntry(relative, CompressionLevel.Fastest);
                    await using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                    await using var output = entry.Open();
                    await input.CopyToAsync(output, token);
                }
            }

            token.ThrowIfCancellationRequested();
            using var commit = OperationExecution.BeginCommit();
            File.Move(temp, target, true);
        }
        finally
        {
            if (File.Exists(temp))
                File.Delete(temp);
        }
    }

    /// <summary>
    /// 导入。
    /// </summary>
    public static async Task<string> Import(string instance, string archive, string? name = null, Action<string>? progress = null, CancellationToken token = default)
    {
        instance = Path.GetFullPath(instance);
        var saves = Path.Combine(instance, "saves");
        Directory.CreateDirectory(saves);
        NoLink(saves);
        // Stage next to the destination, allowing the final directory move to be atomic on this volume.
        var staging = Path.Combine(instance, ".blocklaunch-world-staging");
        Directory.CreateDirectory(staging);
        NoLink(staging);
        var stage = Path.Combine(staging, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        try
        {
            using (var zip = ZipFile.OpenRead(archive))
            {
                if (zip.Entries.Count > MaxEntries)
                    throw new IOException("存档 ZIP 文件数量超过限制。");
                var entries = zip.Entries.Select(e => (Entry: e, Path: e.FullName.Replace('\\', '/'))).ToList();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in entries)
                {
                    token.ThrowIfCancellationRequested();
                    var relative = item.Path.TrimEnd('/');
                    if (relative.Length == 0)
                        throw new IOException("ZIP 包含无效路径。");
                    PackService.SafePath(stage, relative);
                    if (!seen.Add(relative) || ((item.Entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                        throw new IOException("ZIP 包含重复路径或符号链接。");
                }

                var levels = entries.Where(e => !e.Path.EndsWith('/') && (e.Path == "level.dat" || e.Path.EndsWith("/level.dat") && e.Path.Count(c => c == '/') == 1)).ToList();
                if (levels.Count != 1)
                    throw new IOException("ZIP 必须只包含一个存档：level.dat 位于根目录或第一层文件夹内。");
                var prefix = levels[0].Path[..^"level.dat".Length];
                long total = 0;
                foreach (var item in entries)
                {
                    token.ThrowIfCancellationRequested();
                    if (!item.Path.StartsWith(prefix, StringComparison.Ordinal) || item.Path.EndsWith('/'))
                        continue;
                    var relative = item.Path[prefix.Length..];
                    if (relative == "session.lock")
                        continue;
                    if (item.Entry.Length > MaxBytes || (total = checked(total + item.Entry.Length)) > MaxBytes)
                        throw new IOException("存档解压大小超过 8 GiB 限制。");
                    var destination = PackService.SafePath(stage, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    progress?.Invoke("导入存档：" + relative);
                    await using var input = item.Entry.Open();
                    await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    var buffer = new byte[81920];
                    long actual = 0;
                    int read;
                    while ((read = await input.ReadAsync(buffer, token)) > 0)
                    {
                        actual += read;
                        if (actual > item.Entry.Length)
                            throw new IOException("存档 ZIP 大小校验失败。");
                        await output.WriteAsync(buffer.AsMemory(0, read), token);
                    }

                    if (actual != item.Entry.Length)
                        throw new IOException("存档 ZIP 文件不完整。");
                }
            }

            World(stage);
            token.ThrowIfCancellationRequested();
            var display = name ?? Path.GetFileNameWithoutExtension(archive);
            var safeName = new string(display.Where(c => !Path.GetInvalidFileNameChars().Contains(c)).Take(60).ToArray()).Trim(' ', '.');
            if (safeName.Length == 0)
                safeName = "ImportedWorld";
            // Always create a new world, including when a save with this name already exists.
            var destinationWorld = PackService.SafePath(saves, safeName + "-" + Guid.NewGuid().ToString("N")[..8]);
            using var commit = OperationExecution.BeginCommit();
            Directory.Move(stage, destinationWorld);
            return destinationWorld;
        }
        finally
        {
            if (Directory.Exists(stage))
                Directory.Delete(stage, true);
        }
    }
}
