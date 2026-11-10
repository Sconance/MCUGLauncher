using BlockLaunch;
using MCUGLauncher.Contracts;
using MCUGLauncher.Models;
using InstanceDto = MCUGLauncher.Contracts.InstanceInfo;
using ModDto = MCUGLauncher.Contracts.ModInfo;

namespace MCUGLauncher.Services;

// Reads one local snapshot; UI only receives the typed, presentation-neutral result.
internal static class LauncherStateBuilder
{
    public static LauncherState Build(AppConfig config, IReadOnlyList<FavoriteInfo> favorites, AccountStateInfo? account = null)
    {
        var catalog = VersionCatalog.Load(config.Root, config.LastInstance);
        var selectedVersion = catalog.Groups.Count == 0 ? "" :
            catalog.Instances.Any(item => item.Name.Equals(config.LastVersion, StringComparison.OrdinalIgnoreCase))
                ? config.LastVersion : catalog.Selected;
        var mods = VersionCatalog.ReadMods(config.Root, catalog.Selected);
        return new LauncherState(
            ProductInfo.Version,
            account?.Player ?? config.Player,
            account?.AccountType ?? config.AccountType,
            config.Root,
            config.GameRoots.Append(config.Root)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
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
            catalog.Selected,
            catalog.Instances.Select(item => new InstanceDto(
                item.Name,
                item.Loader,
                item.Mods,
                item.Updates,
                item.DisplayName,
                item.Minecraft,
                item.IsPlaceholder)).ToArray(),
            mods.Select(item => new ModDto(
                item.Id,
                item.Name,
                item.File,
                item.Version,
                item.Enabled,
                item.Path,
                item.Size)).ToArray(),
            config.Servers.Select(item => new SavedServerInfo(item.Name, item.Address, item.Instance ?? "")).ToArray(),
            favorites,
            catalog.Groups.Select(group => new VersionGroupInfo(
                group.Label,
                group.Count,
                group.Versions.Select(version => new VersionChoice(
                    version.Length > 0 ? version[0] : "",
                    version.Length > 1 ? version[1] : "")).ToArray())).ToArray(),
            ToSet(config.DataSources),
            ToSet(new DataSourceConfig()),
            ToSet(config.CustomDataSources ?? config.DataSources),
            config.EffectiveDataSourceMode,
            config.LegacyPreferences.Launch.AutoChinese);
    }

    static DataSourceSet ToSet(DataSourceConfig sources) => new(
        sources.Announcements,
        sources.Servers,
        sources.Recommendations,
        sources.Quotes);
}
