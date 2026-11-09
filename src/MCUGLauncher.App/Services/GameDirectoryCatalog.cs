using BlockLaunch;
using System.Text.Json;
using MCUGLauncher.Contracts;
using MCUGLauncher.Models;
using InstanceDto = MCUGLauncher.Contracts.InstanceInfo;

namespace MCUGLauncher.Services;

/// <summary>枚举已保存根目录并隔离每个目录的扫描错误，不改动目录或配置。</summary>
/// <remarks>B 类。依赖 AppConfig.Root/GameRoots、VersionCatalog.Load(string,string) 的
/// Instances、Models.InstanceInfo 的 Name/Loader/Mods/Updates/DisplayName/Minecraft/IsPlaceholder，
/// Contracts.GameDirectoryInfo 和 InstanceInfo 构造函数。路径规则复用 Core.GameDirectoryPath。
/// 不依赖 UseCases、UI、网络或任务队列。详见 docs/components/GameDirectories.md。</remarks>
public static class GameDirectoryCatalog
{
    public static IReadOnlyList<string> Roots(AppConfig config) => config.GameRoots.Prepend(config.Root)
        .Where(path => !string.IsNullOrWhiteSpace(path))
        .Select(path => { try { return GameDirectoryPath.Normalize(path); } catch (ArgumentException) { return path; } })
        .Distinct(GameDirectoryPath.Comparer).ToArray();

    public static string RequireRoot(AppConfig config, string? requested)
    {
        var root = GameDirectoryPath.Normalize(string.IsNullOrWhiteSpace(requested) ? config.Root : requested);
        if (!Roots(config).Contains(root, GameDirectoryPath.Comparer))
            throw new ArgumentException("该游戏目录尚未添加或已移除。");
        return root;
    }

    public static IReadOnlyList<GameDirectoryInfo> Scan(AppConfig config, CancellationToken token = default)
    {
        var results = new List<GameDirectoryInfo>();
        foreach (var candidate in Roots(config))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var root = GameDirectoryPath.Normalize(candidate);
                if (!Directory.Exists(root))
                    throw new DirectoryNotFoundException("目录不可用，可能不存在、未连接或无访问权限。");
                var catalog = VersionCatalog.Load(root, "");
                results.Add(new(root, true, "", catalog.Instances.Select(item => new InstanceDto(
                    item.Name, item.Loader, item.Mods, item.Updates, item.DisplayName, item.Minecraft, item.IsPlaceholder)
                {
                    Root = root
                }).ToArray()));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or System.Security.SecurityException or JsonException or InvalidOperationException or FormatException)
            {
                results.Add(new(candidate, false, error.Message, []));
            }
        }
        return results;
    }
}
