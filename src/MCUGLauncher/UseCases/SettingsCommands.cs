using System.Text.Json;
using BlockLaunch;
using static MCUGLauncher.UseCases.ProtocolValues;

namespace MCUGLauncher.UseCases;

// Handles settings use cases; no WPF controls or browser access.
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
        if (root.TryGetProperty("player", out var player))
            context.Config.Player = player.GetString() ?? context.Config.Player;
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
        if (root.TryGetProperty("java", out var java))
            context.Config.Java = java.GetString()?.Trim() ?? "";
        if (root.TryGetProperty("width", out var width) && width.TryGetInt32(out var widthValue))
            context.Config.Width = Math.Clamp(widthValue, 640, 7680);
        if (root.TryGetProperty("height", out var height) && height.TryGetInt32(out var heightValue))
            context.Config.Height = Math.Clamp(heightValue, 480, 4320);
        context.Store.Save(context.Config);
        context.Send(new
        {
            type = "settingsSaved"
        });
        context.RefreshState();
    }

    public async Task ScanJavaAsync()
    {
        var list = await JavaDiscovery.Scan(context.Config.Root, new[] { context.Config.Java }, context.Token);
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
