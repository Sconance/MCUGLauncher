using BlockLaunch;
using MCUGLauncher.Contracts;
using MCUGLauncher.Models;
using ModDto = MCUGLauncher.Contracts.ModInfo;

namespace MCUGLauncher.Services;

/// <summary>组合多目录扫描和当前目录兼容状态，界面只接收 DTO。</summary>
/// <remarks>B 类。读取 AppConfig 状态字段，调用 GameDirectoryCatalog.Scan(AppConfig,CancellationToken)/Roots(AppConfig)、
/// VersionCatalog.ReadMods(string,string)，构造 Contracts.LauncherState/VersionGroupInfo/ModInfo/SavedServerInfo/DataSourceSet；
/// 不依赖 Commands、窗口或网络。详见 docs/components/GameDirectories.md。</remarks>
internal static class LauncherStateBuilder
{
    public static LauncherState Build(AppConfig config, IReadOnlyList<FavoriteInfo> favorites)
    {
        var directories = GameDirectoryCatalog.Scan(config);
        var activeRoot = GameDirectoryCatalog.Roots(config).FirstOrDefault() ?? config.Root;
        var current = directories.FirstOrDefault(item => GameDirectoryPath.Comparer.Equals(item.Root, activeRoot));
        var instances = current?.Instances ?? [];
        var selected = instances.FirstOrDefault(item => item.Name.Equals(config.LastInstance, StringComparison.OrdinalIgnoreCase))?.Name
            ?? instances.FirstOrDefault()?.Name ?? "";
        var selectedVersion = instances.Any(item => item.Name.Equals(config.LastVersion, StringComparison.OrdinalIgnoreCase)) ? config.LastVersion : selected;
        IReadOnlyList<MCUGLauncher.Models.ModInfo> mods;
        try
        {
            mods = VersionCatalog.ReadMods(activeRoot, selected);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { mods = []; }
        return new LauncherState(
            ProductInfo.Version,
            config.Player,
            config.AccountType,
            activeRoot,
            GameDirectoryCatalog.Roots(config),
            config.Java,
            config.Width,
            config.Height,
            config.Ram,
            config.AutoMemory,
            config.Anonymous,
            !string.IsNullOrWhiteSpace(config.CommunityKey),
            !string.IsNullOrWhiteSpace(config.AdminKey),
            config.SkinStation,
            config.Cape,
            selectedVersion,
            selected,
            instances,
            mods.Select(item => new ModDto(
                item.Id,
                item.Name,
                item.File,
                item.Version,
                item.Enabled,
                item.Path,
                item.Size)).ToArray(),
            config.Servers.Select(item => new SavedServerInfo(item.Name, item.Address, item.Instance ?? "", item.GameRoot ?? "")).ToArray(),
            favorites,
            instances.GroupBy(item => item.Loader).OrderBy(group => group.Key == "原版" ? 0 : 1).ThenBy(group => group.Key)
                .Select(group => new VersionGroupInfo(group.Key == "原版" ? "原版游戏" : group.Key + " 版本", group.Count(),
                    group.Select(item => new VersionChoice(item.Name, item.Minecraft.Length == 0 ? item.Loader : item.Minecraft + " · " + item.Loader.ToLowerInvariant())).ToArray())).ToArray(),
            ToSet(config.DataSources),
            ToSet(new DataSourceConfig()),
            ToSet(config.CustomDataSources ?? config.DataSources),
            config.EffectiveDataSourceMode,
            config.LegacyPreferences.Launch.AutoChinese)
        {
            GameDirectories = directories
        };
    }

    static DataSourceSet ToSet(DataSourceConfig sources) => new(
        sources.Announcements,
        sources.Servers,
        sources.Recommendations,
        sources.Quotes);
}
