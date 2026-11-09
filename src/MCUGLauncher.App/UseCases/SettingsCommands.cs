using System.Text.Json;
using BlockLaunch;
using static MCUGLauncher.UseCases.ProtocolValues;
using MCUGLauncher.Contracts;
using MCUGLauncher.Services;

namespace MCUGLauncher.UseCases;

/// <summary>设置与受管理游戏目录的增删、选择；不删除游戏数据。</summary>
/// <remarks>B 类。依赖 LauncherContext.Config、Store.Save(AppConfig)、RefreshState 和 Token；
/// GameDirectoryCatalog.Roots(AppConfig)/RequireRoot(AppConfig,string)、InstanceSnapshot.Capture(AppConfig,string,string)
/// 用于路径校验和明确实例选择；既有 JavaDiscovery.Scan 依赖不变。
/// 不依赖 UI 或其他 Commands。详见 docs/components/GameDirectories.md。</remarks>
internal sealed class SettingsCommands(LauncherContext context)
{
    void PinFavoriteRoots()
    {
        foreach (var server in context.Config.Servers.Where(server => !string.IsNullOrWhiteSpace(server.Instance) && string.IsNullOrWhiteSpace(server.GameRoot)))
            server.GameRoot = context.Config.Root;
    }

    public SavedInfo AddGameDirectory(string path, bool select)
    {
        var root = GameDirectoryPath.Normalize(path);
        if (!Directory.Exists(root))
            throw new ArgumentException("游戏目录不存在或不可访问。");
        using (var entries = Directory.EnumerateFileSystemEntries(root).GetEnumerator())
            entries.MoveNext();
        PinFavoriteRoots();
        context.Config.GameRoots = GameDirectoryCatalog.Roots(context.Config).Append(root).Distinct(GameDirectoryPath.Comparer).ToList();
        if (select || string.IsNullOrWhiteSpace(context.Config.Root))
        {
            context.Config.Root = root;
            context.Config.LastInstance = context.Config.LastVersion = "";
        }
        context.Store.Save(context.Config);
        context.RefreshState();
        return new("游戏目录已添加。");
    }

    public SavedInfo RemoveGameDirectory(string path)
    {
        var root = GameDirectoryCatalog.RequireRoot(context.Config, path);
        var roots = GameDirectoryCatalog.Roots(context.Config).Where(item => !GameDirectoryPath.Comparer.Equals(item, root)).ToList();
        if (roots.Count == 0)
            throw new ArgumentException("请至少保留一个游戏目录。");
        PinFavoriteRoots();
        context.Config.GameRoots = roots;
        if (GameDirectoryPath.Comparer.Equals(GameDirectoryPath.Normalize(context.Config.Root), root))
        {
            context.Config.Root = roots[0];
            context.Config.LastInstance = context.Config.LastVersion = "";
        }
        context.Store.Save(context.Config);
        context.RefreshState();
        return new("已移除目录记录，游戏文件未删除；已排队任务仍使用原目标。");
    }

    public SavedInfo SelectManagedInstance(InstanceTarget target)
    {
        var instance = InstanceSnapshot.Capture(context.Config, target.Name, target.Root);
        PinFavoriteRoots();
        context.Config.GameRoots = GameDirectoryCatalog.Roots(context.Config).ToList();
        context.Config.Root = instance.Root;
        context.Config.LastInstance = context.Config.LastVersion = instance.Key;
        context.Store.Save(context.Config);
        context.RefreshState();
        return new("实例已选择。");
    }

    public SavedInfo SaveSettings(JsonElement root)
    {
        // Validate the directory before changing any setting in this message.
        var requestedRoot = ReadString(root, "root").Trim();
        if (requestedRoot.Length > 0)
        {
            requestedRoot = GameDirectoryPath.Normalize(requestedRoot);
            if (!Directory.Exists(requestedRoot))
                throw new ArgumentException("游戏目录不存在，请选择已有文件夹。");
        }
        if (root.TryGetProperty("player", out var player))
        {
            if (context.Config.AccountType != "offline")
                throw new ArgumentException("请先切换到离线模式再修改离线玩家名。");
            context.Config.Player = player.GetString() ?? context.Config.Player;
            context.Config.OfflinePlayer = context.Config.Player;
        }
        if (root.TryGetProperty("anonymous", out var anonymous))
            context.Config.Anonymous = anonymous.GetBoolean();
        if (root.TryGetProperty("dataSources", out var sources))
        {
            context.Config.DataSources.Announcements = ReadString(sources, "announcements");
            context.Config.DataSources.Servers = ReadString(sources, "servers");
            context.Config.DataSources.Recommendations = ReadString(sources, "recommendations");
            context.Config.DataSources.Quotes = ReadString(sources, "quotes");
        }
        if (root.TryGetProperty("autoMemory", out var autoMemory))
            context.Config.AutoMemory = autoMemory.GetBoolean();
        if (root.TryGetProperty("autoChinese", out var autoChinese))
            context.Config.LegacyPreferences.Launch.AutoChinese = autoChinese.GetBoolean();
        if (root.TryGetProperty("ram", out var ram) && ram.TryGetInt32(out var ramValue))
            context.Config.Ram = Math.Clamp(ramValue, LaunchLimits.RamMin, LaunchLimits.RamMax);
        if (requestedRoot.Length > 0)
        {
            PinFavoriteRoots();
            if (!string.IsNullOrWhiteSpace(context.Config.Root))
                context.Config.GameRoots.Add(context.Config.Root);
            context.Config.GameRoots.Add(requestedRoot);
            context.Config.GameRoots = GameDirectoryCatalog.Roots(context.Config).ToList();
            context.Config.Root = requestedRoot;
        }
        if (root.TryGetProperty("java", out var java))
            context.Config.Java = java.GetString()?.Trim() ?? "";
        if (root.TryGetProperty("width", out var width) && width.TryGetInt32(out var widthValue))
            context.Config.Width = Math.Clamp(widthValue, LaunchLimits.WindowWidthMin, LaunchLimits.WindowWidthMax);
        if (root.TryGetProperty("height", out var height) && height.TryGetInt32(out var heightValue))
            context.Config.Height = Math.Clamp(heightValue, LaunchLimits.WindowHeightMin, LaunchLimits.WindowHeightMax);
        context.Store.Save(context.Config);
        context.RefreshState();
        return new("设置已保存。");
    }

    public SavedInfo ResetDataSources()
    {
        return UseDataSources("default");
    }

    static MCUGLauncher.Models.DataSourceConfig CopySources(MCUGLauncher.Models.DataSourceConfig sources) => new()
    {
        Announcements = sources.Announcements,
        Servers = sources.Servers,
        Recommendations = sources.Recommendations,
        Quotes = sources.Quotes
    };

    public SavedInfo SaveCustomSources(JsonElement message)
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
        context.RefreshState();
        return new("自定义数据源已保存。");
    }

    public SavedInfo UseDataSources(string mode)
    {
        if (mode is not ("default" or "custom"))
            throw new ArgumentException("未知数据源类型。");
        context.Config.CustomDataSources ??= CopySources(context.Config.DataSources);
        context.Config.DataSources = mode == "default" ? new() : CopySources(context.Config.CustomDataSources);
        context.Config.DataSourceMode = mode;
        context.Store.Save(context.Config);
        context.RefreshState();
        return new(mode == "default" ? "已切换到默认数据源。" : "已切换到自定义数据源。");
    }

    public async Task<IReadOnlyList<JavaInfo>> ScanJavaAsync()
    {
        var list = await JavaDiscovery.Scan(context.Config.Root, new[] { context.Config.Java }, context.Token);
        return list.Select(item => new JavaInfo(
            item.Path,
            item.Major,
            item.Architecture,
            item.Vendor,
            item.Error)).ToArray();
    }

}
