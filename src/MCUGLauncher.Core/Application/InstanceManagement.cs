using System.IO.Compression;
using System.Text.Json;

namespace BlockLaunch;

/// <summary>
/// 表示 ExportOptions 数据。
/// </summary>
public record ExportOptions(bool Mods, bool Servers, bool Shaders, bool Resources, bool Saves);

/// <summary>
/// 实例导出与删除。
/// </summary>
/// <remarks>
/// 依赖 PackRequirements 与 ExportOptions；导出会打包实例的 mods/服务器/光影/资源/存档，删除会清理实例目录并返回被删除的路径。
/// </remarks>
public static class InstanceManagement
{
    /// <summary>
    /// 导出。
    /// </summary>
    /// <param name="directory">实例目录</param>
    /// <param name="target">导出目标目录</param>
    /// <param name="name">导出实例名</param>
    /// <param name="requirements">版本与加载器要求</param>
    /// <param name="options">要包含的内容</param>
    /// <param name="token">取消令牌</param>
    public static async Task Export(string directory, string target, string name, PackRequirements requirements, ExportOptions options, CancellationToken token, bool overwrite = true)
    {
        var source = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        target = Path.GetFullPath(target);
        if (target.StartsWith(source, StringComparison.OrdinalIgnoreCase))
            throw new IOException("导出位置不能在实例目录内。");
        using var lease = ContentService.LockInstance(directory);
        var temp = target + "." + Guid.NewGuid().ToString("N") + ".partial";
        try
        {
            using (var archive = ZipFile.Open(temp, ZipArchiveMode.Create))
            {
                var components = new List<object>
                {
                    new
                    {
                        uid = "net.minecraft",
                        version = requirements.Minecraft
                    }
                };
                var loader = requirements.Loader switch
                {
                    "fabric" => "net.fabricmc.fabric-loader",
                    "quilt" => "org.quiltmc.quilt-loader",
                    "forge" => "net.minecraftforge",
                    "neoforge" => "net.neoforged",
                    _ => ""
                };
                if (loader.Length > 0)
                    components.Add(new
                    {
                        uid = loader,
                        version = requirements.LoaderVersion
                    });
                async Task Json(string path, object value)
                {
                    await using var s = archive.CreateEntry(path).Open();
                    await JsonSerializer.SerializeAsync(s, value, cancellationToken: token);
                }

                await Json("mmc-pack.json", new
                {
                    formatVersion = 1,
                    components
                });
                await Json("blocklaunch-export.json", new
                {
                    formatVersion = 1,
                    name,
                    requirements,
                    options
                });
                await using (var config = new StreamWriter(archive.CreateEntry("instance.cfg").Open()))
                    await config.WriteAsync("[General]\nname=" + name.Replace('\n', ' ').Replace('\r', ' ') + "\n");
                // Enumerate without following junctions or symlinks; exclude launcher state and private runtime artifacts.
                var enumeration = new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    AttributesToSkip = FileAttributes.ReparsePoint,
                    IgnoreInaccessible = false
                };
                foreach (var file in Directory.EnumerateFiles(directory, "*", enumeration))
                {
                    token.ThrowIfCancellationRequested();
                    var relative = Path.GetRelativePath(directory, file).Replace('\\', '/');
                    var head = relative.Split('/')[0];
                    if (head.StartsWith('.') || head is "accounts" or "logs" or "crash-reports" or "screenshots" or "libraries" or "assets" or "versions" or "runtime" or "natives" || relative is "settings.json" or "microsoft-account.dat" or "launcher_accounts.json" or "launcher_profiles.json" or "usercache.json" || relative.EndsWith(".lock"))
                        continue;
                    if (head == "mods" && !options.Mods || head == "shaderpacks" && !options.Shaders || head == "resourcepacks" && !options.Resources || head == "saves" && !options.Saves || relative == "servers.dat" && !options.Servers || relative == "servers.dat_old")
                        continue;
                    var entry = archive.CreateEntry("minecraft/" + relative, CompressionLevel.Optimal);
                    await using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                    await using var output = entry.Open();
                    await input.CopyToAsync(output, token);
                }
            }

            token.ThrowIfCancellationRequested();
            using var guard = OperationExecution.BeginCommit();
            File.Move(temp, target, overwrite);
        }
        finally
        {
            if (File.Exists(temp))
                File.Delete(temp);
        }
    }

    /// <summary>
    /// 移除。
    /// </summary>
    /// <param name="root">游戏根目录</param>
    /// <param name="key">实例名</param>
    /// <returns>被删除的实例目录</returns>
    public static string Remove(string root, string key)
    {
        ContentService.SafeFile(key);
        var catalog = new InstanceCatalog(root);
        var descriptor = catalog.GetOrCreate(key);
        var versions = Path.Combine(root, "versions");
        foreach (var profile in Directory.GetDirectories(versions).Select(d => Path.Combine(d, Path.GetFileName(d) + ".json")).Where(File.Exists))
        {
            using var json = JsonDocument.Parse(File.ReadAllText(profile));
            if (json.RootElement.TryGetProperty("inheritsFrom", out var parent) && parent.GetString() == key)
                throw new IOException("其他实例依赖此游戏版本，请先移除依赖实例。不会删除共享游戏库。");
        }

        var trash = Path.Combine(root, ".blocklaunch-trash", key + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(trash);
        var metadata = catalog.MetadataDirectory(key);
        var profileDir = Path.Combine(versions, key);
        using var guard = OperationExecution.BeginCommit();
        if (Directory.Exists(profileDir))
            Directory.Move(profileDir, Path.Combine(trash, "version"));
        try
        {
            if (Directory.Exists(metadata))
                Directory.Move(metadata, Path.Combine(trash, "instance"));
        }
        catch
        {
            if (Directory.Exists(Path.Combine(trash, "version")))
                Directory.Move(Path.Combine(trash, "version"), profileDir);
            throw;
        }

        AtomicFile.WriteJson(Path.Combine(trash, "restore.json"), new
        {
            root,
            key,
            descriptor.DataDirectory
        });
        return trash;
    }
}
