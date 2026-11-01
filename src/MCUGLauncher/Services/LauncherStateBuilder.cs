using BlockLaunch;
using MCUGLauncher.Models;

namespace MCUGLauncher.Services;

// Reads one local snapshot; message routing stays in LauncherApplication.
internal static class LauncherStateBuilder
{
    public static object Build(AppConfig config, object favorites)
    {
        var catalog = VersionCatalog.Load(config.Root, config.LastInstance);
        var selectedVersion = catalog.Groups.Count == 0 ? "" :
            catalog.Instances.Any(item => item.Name.Equals(config.LastVersion, StringComparison.OrdinalIgnoreCase))
                ? config.LastVersion : catalog.Selected;

        var mods = VersionCatalog.ReadMods(config.Root, catalog.Selected);
        var state = new
        {
            type = "state",
            appVersion = ProductInfo.Version,
            player = config.Player,
            accountType = config.AccountType,
            accountLabel = config.AccountType switch
            {
                "microsoft" => "正版账号 · 在线验证",
                "mua" => "MUA 账号 · 外置登录",
                _ => "离线账号 · 本地启动"
            },
            root = config.Root,
            gameRoots = config.GameRoots.Append(config.Root).Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            java = config.Java,
            width = config.Width,
            height = config.Height,
            selectedVersion,
            selectedInstance = catalog.Selected,
            ram = config.Ram,
            anonymous = config.Anonymous,
            communityKey = !string.IsNullOrWhiteSpace(config.CommunityKey),
            adminKey = !string.IsNullOrWhiteSpace(config.AdminKey),
            skinStation = config.SkinStation,
            cape = config.Cape,
            versionGroups = catalog.Groups,
            instances = catalog.Instances,
            mods = mods.Select(mod => new
            {
                id = mod.Id,
                name = mod.Name,
                author = "本地模组",
                desc = mod.File,
                tags = new[] { "本地" },
                size = FormatSize(mod.Size),
                downloads = "",
                v = string.IsNullOrWhiteSpace(mod.Version) ? "—" : mod.Version,
                color = mod.Enabled ? "green" : "slate",
                ic = "box",
                file = mod.File,
                path = mod.Path,
                enabled = mod.Enabled,
                update = ""
            }),
            favorites = favorites,
            servers = config.Servers,
            dataSources = new
            {
                announcements = config.DataSources.Announcements,
                servers = config.DataSources.Servers,
                recommendations = config.DataSources.Recommendations,
                quotes = config.DataSources.Quotes
            },
            writeEndpoint = config.WriteEndpoint
        };
        return state;
    }
    static string FormatSize(long bytes) => bytes <= 0 ? "—" : bytes < 1024 * 1024
        ? $"{bytes / 1024.0:0.#} KB" : $"{bytes / 1024.0 / 1024.0:0.#} MB";

}
