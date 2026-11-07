using System.Text.Json;

namespace BlockLaunch;

/// <summary>
/// 表示 CloneOptions 数据。
/// </summary>
public record CloneOptions(bool Saves = false, bool Screenshots = false, bool Servers = false);

/// <summary>
/// 提供 实例Cloner 相关功能。
/// </summary>
public static class InstanceCloner
{
    static void NoLink(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("复制实例暂不支持链接：" + Path.GetFileName(path));
    }

    static IEnumerable<string> Files(string path, CancellationToken token)
    {
        NoLink(path);
        foreach (var entry in Directory.EnumerateFileSystemEntries(path))
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
    /// 复制。
    /// </summary>
    public static async Task<string> Copy(string root, string key, string source, string minecraft, string name, LaunchSettings effectiveSettings, CloneOptions options, Action<string>? progress = null, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
            throw new ArgumentException("实例名称需为 1–100 个字符。");
        ContentService.SafeFile(minecraft);
        root = Path.GetFullPath(root);
        source = Path.GetFullPath(source);
        var catalog = new InstanceCatalog(root);
        var original = catalog.GetOrCreate(key);
        ContentService.SafeFile(original.LaunchProfileId);
        if (!File.Exists(Path.Combine(root, "versions", original.LaunchProfileId, original.LaunchProfileId + ".json")))
            throw new IOException("请先安装源实例所需的游戏版本。");
        NoLink(source);
        using var sourceLock = ContentService.LockInstance(source);
        var newKey = "instance-" + Guid.NewGuid().ToString("N")[..12];
        var parent = Path.Combine(root, ".blocklaunch-clone-staging");
        Directory.CreateDirectory(parent);
        NoLink(parent);
        var stage = Path.Combine(parent, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        var target = catalog.MetadataDirectory(newKey);
        var profileDirectory = Path.Combine(root, "versions", newKey);
        bool moved = false, profileMade = false;
        var worldLocks = new List<FileStream>();
        try
        {
            if (options.Saves && Directory.Exists(Path.Combine(source, "saves")))
            {
                NoLink(Path.Combine(source, "saves"));
                foreach (var world in Directory.EnumerateDirectories(Path.Combine(source, "saves")))
                {
                    NoLink(world);
                    if (!File.Exists(Path.Combine(world, "level.dat")))
                        continue;
                    var session = Path.Combine(world, "session.lock");
                    if (File.Exists(session))
                        NoLink(session);
                    try
                    {
                        worldLocks.Add(new(session, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
                    }
                    catch (IOException ex)
                    {
                        throw new IOException("源存档正在使用，请退出游戏后再复制存档。", ex);
                    }
                }
            }

            var directories = new List<string>
            {
                "mods",
                "config",
                "defaultconfigs",
                "resourcepacks",
                "shaderpacks",
                "kubejs",
                "scripts"
            };
            if (options.Saves)
                directories.Add("saves");
            if (options.Screenshots)
                directories.Add("screenshots");
            var files = new List<string>();
            foreach (var child in directories)
                if (Directory.Exists(Path.Combine(source, child)))
                    files.AddRange(Files(Path.Combine(source, child), token));
            foreach (var child in new[] {
                "options.txt",
                "optionsof.txt",
                "optionsshaders.txt",
                ".blocklaunch-content.json"
            }.Concat(options.Servers ? ["servers.dat"] : Array.Empty<string>()))
                if (File.Exists(Path.Combine(source, child)))
                {
                    NoLink(Path.Combine(source, child));
                    files.Add(Path.Combine(source, child));
                }

            long total = 0;
            if (files.Count > 100_000)
                throw new IOException("复制文件超过 10 万个。");
            foreach (var file in files)
            {
                token.ThrowIfCancellationRequested();
                var relative = Path.GetRelativePath(source, file).Replace('\\', '/');
                if (relative.EndsWith(".lock", StringComparison.OrdinalIgnoreCase))
                    continue;
                var length = new FileInfo(file).Length;
                total = checked(total + length);
                if (total > 20L * 1024 * 1024 * 1024)
                    throw new IOException("复制数据超过 20 GiB。");
                var destination = PackService.SafePath(stage, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                progress?.Invoke("复制实例：" + relative);
                await using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                var buffer = new byte[81920];
                long copied = 0;
                int read;
                while ((read = await input.ReadAsync(buffer, token)) > 0)
                {
                    copied += read;
                    if (copied > length)
                        throw new IOException("复制过程中源文件发生变化，请退出游戏再重试。");
                    await output.WriteAsync(buffer.AsMemory(0, read), token);
                }

                if (copied != length)
                    throw new IOException("复制过程中源文件发生变化。");
            }

            var settings = JsonSerializer.Deserialize<LaunchSettings>(JsonSerializer.Serialize(effectiveSettings))!;
            settings.Isolation = "独立实例";
            var descriptor = new InstanceDescriptor
            {
                Key = newKey,
                DisplayName = name.Trim(),
                LaunchProfileId = original.LaunchProfileId,
                Description = original.Description,
                Category = original.Category,
                Launch = settings
            };
            if (File.Exists(original.IconPath))
            {
                NoLink(original.IconPath);
                var icon = "instance-icon" + Path.GetExtension(original.IconPath);
                File.Copy(original.IconPath, Path.Combine(stage, icon));
                descriptor.IconPath = Path.Combine(target, icon);
            }

            AtomicFile.WriteJson(Path.Combine(stage, ".blocklaunch-instance.json"), descriptor);
            var packFile = Path.Combine(catalog.MetadataDirectory(key), ".blocklaunch-pack.json");
            if (File.Exists(packFile))
            {
                var pack = AtomicFile.ReadJson<InstalledPack>(packFile);
                AtomicFile.WriteJson(Path.Combine(stage, ".blocklaunch-pack.json"), pack with
                {
                    Name = name.Trim(),
                    InstanceId = newKey,
                    LaunchVersion = original.LaunchProfileId
                });
            }

            var baseline = Path.Combine(catalog.MetadataDirectory(key), ".blocklaunch-pack-files.json");
            if (File.Exists(baseline))
            {
                NoLink(baseline);
                File.Copy(baseline, Path.Combine(stage, ".blocklaunch-pack-files.json"));
            }

            token.ThrowIfCancellationRequested();
            using var commit = OperationExecution.BeginCommit();
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            Directory.Move(stage, target);
            moved = true;
            if (Directory.Exists(profileDirectory))
                throw new IOException("新实例版本标识冲突，请重试。");
            Directory.CreateDirectory(profileDirectory);
            profileMade = true;
            AtomicFile.WriteJson(Path.Combine(profileDirectory, newKey + ".json"), new
            {
                id = newKey,
                inheritsFrom = original.LaunchProfileId,
                jar = minecraft,
                type = "release"
            });
            return newKey;
        }
        catch
        {
            if (profileMade && Directory.Exists(profileDirectory))
                Directory.Delete(profileDirectory, true);
            if (moved && Directory.Exists(target))
                Directory.Move(target, stage);
            throw;
        }
        finally
        {
            foreach (var lease in worldLocks)
                lease.Dispose();
            if (Directory.Exists(stage))
                Directory.Delete(stage, true);
        }
    }
}
