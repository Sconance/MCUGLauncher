using MCUGLauncher.Contracts;
using MCUGLauncher.Models;

namespace MCUGLauncher.Services;

/// <summary>Build one typed snapshot; mutable configuration never escapes to a View.</summary>
/// <remarks>B: reads AppConfig fields, VersionCatalog.Load/ReadMods and supplied favorite snapshots. No UI or protocol parsing.</remarks>
internal static class LauncherStateBuilder
{
    public static LauncherState Build(AppConfig config, Contracts.ResourceInfo[] favorites)
    {
        var catalog = VersionCatalog.Load(config.Root, config.LastInstance);
        var selectedVersion = catalog.Groups.Count == 0 ? "" :
            catalog.Instances.Any(item => item.Name.Equals(config.LastVersion, StringComparison.OrdinalIgnoreCase)) ? config.LastVersion : catalog.Selected;
        var prefs = config.LegacyPreferences;
        return new LauncherState
        {
            AppVersion = ProductInfo.Version,
            Player = config.Player,
            AccountType = config.AccountType,
            Root = config.Root,
            GameRoots = config.GameRoots.Append(config.Root).Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            Java = config.Java,
            Width = config.Width,
            Height = config.Height,
            Ram = config.Ram,
            AutoMemory = config.AutoMemory,
            SelectedVersion = selectedVersion,
            SelectedInstance = catalog.Selected,
            Anonymous = config.Anonymous,
            CommunityKey = !string.IsNullOrWhiteSpace(config.CommunityKey),
            AdminKey = !string.IsNullOrWhiteSpace(config.AdminKey),
            SkinStation = config.SkinStation,
            Cape = config.Cape,
            WriteEndpoint = config.WriteEndpoint,
            Preferences = new PreferencesInfo
            {
                BackgroundPath = prefs.BackgroundPath,
                BackgroundFit = prefs.BackgroundFit,
                BackgroundOpacity = prefs.BackgroundOpacity,
                SaveBackupCount = prefs.SaveBackupCount,
                ResourceDownloadDirectory = prefs.ResourceDownloadDirectory,
                Network = new NetworkSettingsInfo { DownloadSource = prefs.Network.DownloadSource, ProxyMode = prefs.Network.ProxyMode, ProxyAddress = prefs.Network.ProxyAddress, LimitKibPerSecond = prefs.Network.LimitKibPerSecond }
            },
            Instances = catalog.Instances.Select(i => new Contracts.InstanceInfo { Id = i.Name, Name = i.Name, DisplayName = i.DisplayName, Minecraft = i.Minecraft, Loader = i.Loader }).ToArray(),
            Mods = VersionCatalog.ReadMods(config.Root, catalog.Selected).Select(m => new Contracts.ModInfo { Id = m.Id, Name = m.Name, File = m.File, Version = m.Version, Enabled = m.Enabled, Path = m.Path, Size = m.Size }).ToArray(),
            VersionGroups = catalog.Groups.Select(g => new VersionGroupInfo(g.Label, g.Count, g.Versions)).ToArray(),
            Favorites = favorites,
            Servers = config.Servers.Select(s => new ServerInfo { Name = s.Name, Address = s.Address }).ToArray(),
            DataSources = Sources(config.DataSources),
            DefaultDataSources = Sources(new()),
            CustomDataSources = Sources(config.CustomDataSources ?? config.DataSources),
            DataSourceMode = config.EffectiveDataSourceMode
        };
    }
    static DataSourceSet Sources(DataSourceConfig source) => new() { Announcements = source.Announcements, Servers = source.Servers, Recommendations = source.Recommendations, Quotes = source.Quotes };
}
