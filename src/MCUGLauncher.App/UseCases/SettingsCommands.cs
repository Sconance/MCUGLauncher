using System.Text.Json;
using MCUGLauncher;
using static MCUGLauncher.UseCases.ProtocolValues;

namespace MCUGLauncher.UseCases;

/// <summary>验证并保存全局配置、网络及外观偏好，提供 Java 扫描。</summary>
/// <remarks>
/// B 类。依赖 LauncherContext.Config（Root/GameRoots/Player/OfflinePlayer/AccountType/Anonymous/WriteEndpoint/DataSources/AutoMemory/Ram/Java/Width/Height/LegacyPreferences）、
/// Store.Save(AppConfig)、Send(object)、RefreshState()、Token；JavaDiscovery.Scan(string,IEnumerable&lt;string&gt;,CancellationToken)。
/// Core NetworkSettings.Validate 与配置类型用于值验证。无窗口、视图或其他 Commands 依赖。
/// </remarks>
internal sealed class SettingsCommands(LauncherContext context)
{
    public void SaveSettings(JsonElement root)
    {
        // Validate the directory before changing any setting in this message.
        var requestedRoot = ReadString(root, "root").Trim();
        if (requestedRoot.Length > 0)
        {
            requestedRoot = Path.GetFullPath(requestedRoot);
            if (!Directory.Exists(requestedRoot))
                throw new ArgumentException("游戏目录不存在，请选择已有文件夹。");
        }
        var preferences = ReadPreferences(root);
        var roots = root.TryGetProperty("gameRoots", out var requestedRoots)
            ? requestedRoots.EnumerateArray().Select(value => Path.GetFullPath(value.GetString() ?? "")).Distinct(StringComparer.OrdinalIgnoreCase).ToList() : null;
        if (roots?.Any(path => !Directory.Exists(path)) == true)
            throw new ArgumentException("目录列表包含不存在的文件夹。");
        if (root.TryGetProperty("player", out var player))
        {
            if (context.Config.AccountType != "offline")
                throw new ArgumentException("请先切换到离线模式再修改离线玩家名。");
            context.Config.Player = player.GetString() ?? context.Config.Player;
            context.Config.OfflinePlayer = context.Config.Player;
        }
        if (root.TryGetProperty("anonymous", out var anonymous))
            context.Config.Anonymous = anonymous.GetBoolean();
        if (root.TryGetProperty("writeEndpoint", out var endpoint))
            context.Config.WriteEndpoint = endpoint.GetString() ?? "";
        if (root.TryGetProperty("dataSources", out var sources))
        {
            context.Config.DataSources.Announcements = ReadString(sources, "announcements");
            context.Config.DataSources.Servers = ReadString(sources, "servers");
            context.Config.DataSources.Recommendations = ReadString(sources, "recommendations");
            context.Config.DataSources.Quotes = ReadString(sources, "quotes");
        }
        if (root.TryGetProperty("autoMemory", out var autoMemory))
            context.Config.AutoMemory = autoMemory.GetBoolean();
        if (root.TryGetProperty("ram", out var ram) && ram.TryGetInt32(out var ramValue))
            context.Config.Ram = Math.Clamp(ramValue, 512, 65536);
        if (requestedRoot.Length > 0)
        {
            if (!string.IsNullOrWhiteSpace(context.Config.Root))
                context.Config.GameRoots.Add(context.Config.Root);
            context.Config.GameRoots.Add(requestedRoot);
            context.Config.GameRoots = context.Config.GameRoots.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            context.Config.Root = requestedRoot;
        }
        if (roots != null)
            context.Config.GameRoots = roots.Append(context.Config.Root).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (root.TryGetProperty("java", out var java))
        {
            context.Config.Java = java.GetString()?.Trim() ?? "";
            if (File.Exists(context.Config.Java))
                preferences.ExtraJavaPaths = preferences.ExtraJavaPaths.Append(context.Config.Java).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
        if (root.TryGetProperty("width", out var width) && width.TryGetInt32(out var widthValue))
            context.Config.Width = Math.Clamp(widthValue, 640, 7680);
        if (root.TryGetProperty("height", out var height) && height.TryGetInt32(out var heightValue))
            context.Config.Height = Math.Clamp(heightValue, 480, 4320);
        // Keep the network object used by the active HTTP policy; proxy handlers change after restart.
        var currentNetwork = context.Config.LegacyPreferences.Network;
        currentNetwork.DownloadSource = preferences.Network.DownloadSource;
        currentNetwork.LimitKibPerSecond = preferences.Network.LimitKibPerSecond;
        currentNetwork.ProxyMode = preferences.Network.ProxyMode;
        currentNetwork.ProxyAddress = preferences.Network.ProxyAddress;
        preferences.Network = currentNetwork;
        context.Config.LegacyPreferences = preferences;
        context.Store.Save(context.Config);
        context.Send(new
        {
            type = "settingsSaved"
        });
        context.RefreshState();
    }

    MCUGLauncher.Preferences ReadPreferences(JsonElement message)
    {
        var preferences = JsonSerializer.Deserialize<MCUGLauncher.Preferences>(JsonSerializer.Serialize(context.Config.LegacyPreferences))!;
        if (message.TryGetProperty("network", out var network))
        {
            preferences.Network = new NetworkSettings
            {
                DownloadSource = ReadString(network, "downloadSource"),
                ProxyMode = ReadString(network, "proxyMode"),
                ProxyAddress = ReadString(network, "proxyAddress").Trim(),
                LimitKibPerSecond = network.GetProperty("limitKibPerSecond").GetInt32()
            };
            preferences.Network.Validate();
        }
        if (message.TryGetProperty("appearance", out var appearance))
        {
            var path = ReadString(appearance, "path").Trim();
            if (path.Length > 0 && (!File.Exists(path) || Path.GetExtension(path).ToLowerInvariant() is not (".png" or ".jpg" or ".jpeg")))
                throw new ArgumentException("请选择现有的 PNG 或 JPEG 背景图片。");
            var opacity = appearance.GetProperty("opacity").GetDouble();
            var fit = ReadString(appearance, "fit");
            if (!double.IsFinite(opacity) || opacity is < 0 or > 1 || fit is not ("裁剪填充" or "完整显示" or "拉伸"))
                throw new ArgumentException("背景透明度或显示方式无效。");
            preferences.BackgroundPath = path.Length > 0 ? Path.GetFullPath(path) : "";
            preferences.BackgroundOpacity = opacity;
            preferences.BackgroundFit = fit;
        }
        if (message.TryGetProperty("backupCount", out var count))
        {
            var value = count.GetInt32();
            if (value is < 1 or > 50)
                throw new ArgumentException("每个实例保留的备份数量应为 1–50。");
            preferences.SaveBackupCount = value;
        }
        if (message.TryGetProperty("resourceDownloadDirectory", out var directory))
        {
            var path = Path.GetFullPath(directory.GetString() ?? "");
            if (File.Exists(path))
                throw new ArgumentException("下载位置必须是文件夹。");
            preferences.ResourceDownloadDirectory = path;
        }
        return preferences;
    }

    public void ResetDataSources()
    {
        UseDataSources("default");
    }

    static MCUGLauncher.Models.DataSourceConfig CopySources(MCUGLauncher.Models.DataSourceConfig sources) => new()
    {
        Announcements = sources.Announcements,
        Servers = sources.Servers,
        Recommendations = sources.Recommendations,
        Quotes = sources.Quotes
    };

    public void SaveCustomSources(JsonElement message)
    {
        var sources = message.GetProperty("dataSources");
        // Retain the active mode before saving a separate editable preset.
        context.Config.DataSourceMode = context.Config.EffectiveDataSourceMode;
        context.Config.CustomDataSources = new()
        {
            Announcements = ReadString(sources, "announcements").Trim(),
            Servers = ReadString(sources, "servers").Trim(),
            Recommendations = ReadString(sources, "recommendations").Trim(),
            Quotes = ReadString(sources, "quotes").Trim()
        };
        context.Store.Save(context.Config);
        context.Send(new
        {
            type = "settingsSaved"
        });
        context.RefreshState();
    }

    public void UseDataSources(string mode)
    {
        if (mode is not ("default" or "custom"))
            throw new ArgumentException("未知数据源类型。");
        context.Config.CustomDataSources ??= CopySources(context.Config.DataSources);
        context.Config.DataSources = mode == "default" ? new() : CopySources(context.Config.CustomDataSources);
        context.Config.DataSourceMode = mode;
        context.Store.Save(context.Config);
        context.RefreshState();
    }

    public async Task ScanJavaAsync()
    {
        var list = await JavaDiscovery.Scan(context.Config.Root, context.Config.LegacyPreferences.ExtraJavaPaths.Prepend(context.Config.Java), context.Token);
        context.Send(new
        {
            type = "javaList",
            items = list.Select(item => new
            {
                path = item.Path,
                major = item.Major,
                arch = item.Architecture,
                vendor = item.Vendor,
                error = item.Error
            })
        });
    }

}
