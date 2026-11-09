using System.Text.Json;
using BlockLaunch;
using MCUGLauncher.Contracts;
using MCUGLauncher.Models;
using InstanceDto = MCUGLauncher.Contracts.InstanceInfo;
using InstanceModel = MCUGLauncher.Models.InstanceInfo;
using ModDto = MCUGLauncher.Contracts.ModInfo;

namespace MCUGLauncher.Services;

// Reads one local snapshot; UI only receives the typed, presentation-neutral result.
internal static class LauncherStateBuilder
{
    public static LauncherState Build(AppConfig config, IReadOnlyList<FavoriteInfo> favorites)
    {
        var catalog = VersionCatalog.Load(config.Root, config.LastInstance);
        var selectedVersion = catalog.Groups.Count == 0
            ? ""
            : catalog.Instances.Any(item => item.Name.Equals(config.LastVersion, StringComparison.OrdinalIgnoreCase))
                ? config.LastVersion
                : catalog.Selected;
        var mods = VersionCatalog.ReadMods(config.Root, catalog.Selected);
        var instances = catalog.Instances.Select(item => ToInstance(config.Root, item)).ToArray();
        return new LauncherState(
            ProductInfo.Version,
            config.Player,
            config.AccountType,
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
            instances,
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

    /// <summary>
    /// 版本号与类型由 <see cref="VersionCatalog"/> 按 PCL2 规则解析好；这里只补加载器版本，
    /// 并在版本号缺失时用实例元数据兜底。元数据识别的加载器与目录扫描不一致时，
    /// 加载器版本可能属于另一个加载器，宁可不写。
    /// </summary>
    static InstanceDto ToInstance(string root, InstanceModel source)
    {
        var inspected = Inspect(root, source.Name);
        return new InstanceDto(
            source.Name,
            source.Loader,
            source.Mods,
            source.Updates,
            source.DisplayName,
            source.Minecraft.Length > 0 ? source.Minecraft : inspected?.Minecraft ?? "",
            source.IsPlaceholder,
            inspected is not null && inspected.Loader == LoaderKind(source.Loader) ? inspected.LoaderVersion : "",
            source.VersionType);
    }

    static PackRequirements? Inspect(string root, string instance)
    {
        try
        {
            return InstanceRequirements.Read(root, instance);
        }
        catch (Exception error) when (error is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    static string LoaderKind(string loader) => loader switch
    {
        "Fabric" => "fabric",
        "Forge" => "forge",
        "NeoForge" => "neoforge",
        "Quilt" => "quilt",
        "OptiFine" => "optifine",
        _ => "vanilla"
    };
}
