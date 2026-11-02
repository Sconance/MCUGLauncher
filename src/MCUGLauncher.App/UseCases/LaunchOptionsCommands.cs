using System.Text.Json;
using BlockLaunch;
using MCUGLauncher.Services;
using static MCUGLauncher.UseCases.ProtocolValues;
using MCUGLauncher.Contracts;

namespace MCUGLauncher.UseCases;

internal sealed class LaunchOptionsCommands(LauncherContext context)
{
    sealed record Target(string Root, string Version);
    readonly PreviewCache<Target> targets = new();

    public void Read(JsonElement message)
    {
        var version = ReadString(message, "version");
        var root = context.Config.Root;
        var inherited = true;
        LaunchSettings settings;
        var name = "";
        if (version.Length > 0)
        {
            var snapshot = InstanceSnapshot.Capture(context.Config, version);
            var descriptor = new InstanceCatalog(root).GetOrCreate(version);
            inherited = descriptor.Launch == null;
            settings = snapshot.Settings;
            name = descriptor.DisplayName;
        }
        else
        {
            settings = Clone(context.Config.LegacyPreferences.Launch);
            settings.AutoMemory = context.Config.AutoMemory;
            settings.RamMb = context.Config.Ram;
            settings.Java = context.Config.Java;
            settings.Width = context.Config.Width;
            settings.Height = context.Config.Height;
        }
        context.Send(new
        {
            type = "launchOptions",
            requestId = ReadString(message, "requestId"),
            token = targets.Add(new(root, version)),
            version,
            name,
            inherited,
            settings
        });
    }

    public void Save(JsonElement message)
    {
        var token = ReadString(message, "token");
        var target = targets.Get(token);
        var options = message.GetProperty("settings");
        var inherited = message.TryGetProperty("inherit", out var inherit) && inherit.GetBoolean();
        var settings = Clone(context.Config.LegacyPreferences.Launch);
        if (target.Version.Length > 0)
            settings = InstanceSnapshot.Capture(ConfigAt(target.Root), target.Version).Settings;
        settings.AutoMemory = options.GetProperty("autoMemory").GetBoolean();
        settings.FullScreen = options.GetProperty("fullScreen").GetBoolean();
        settings.RamMb = Number(options, "ramMb", LaunchLimits.RamMin, LaunchLimits.RamMax);
        settings.Width = Number(options, "width", LaunchLimits.WindowWidthMin, LaunchLimits.WindowWidthMax);
        settings.Height = Number(options, "height", LaunchLimits.WindowHeightMin, LaunchLimits.WindowHeightMax);
        settings.Java = ReadString(options, "java").Trim();
        settings.JvmArguments = ReadString(options, "jvmArguments");
        settings.WindowTitle = ReadString(options, "windowTitle");
        settings.Server = ReadString(options, "server").Trim();
        LaunchSettings.SplitArguments(settings.JvmArguments);
        if (target.Version.Length == 0)
        {
            context.Config.LegacyPreferences.Launch = settings;
            context.Config.AutoMemory = settings.AutoMemory;
            context.Config.Java = settings.Java;
            context.Config.Ram = settings.RamMb;
            context.Config.Width = settings.Width;
            context.Config.Height = settings.Height;
            context.Store.Save(context.Config);
        }
        else
        {
            var catalog = new InstanceCatalog(target.Root);
            var descriptor = catalog.GetOrCreate(target.Version);
            var name = ReadString(message, "name").Trim();
            if (name.Length == 0 || name.Length > LaunchLimits.DisplayNameMax)
                throw new ArgumentException("实例显示名称需为 1–" + LaunchLimits.DisplayNameMax + " 个字符。");
            descriptor.DisplayName = name;
            descriptor.Launch = inherited ? null : settings;
            catalog.Save(descriptor);
        }
        targets.Get(token, true);
        context.Send(new
        {
            type = "launchOptionsSaved"
        });
        context.RefreshState();
    }

    MCUGLauncher.Models.AppConfig ConfigAt(string root)
    {
        var config = JsonSerializer.Deserialize<MCUGLauncher.Models.AppConfig>(JsonSerializer.Serialize(context.Config))!;
        config.Root = root;
        return config;
    }
    static LaunchSettings Clone(LaunchSettings settings) => JsonSerializer.Deserialize<LaunchSettings>(JsonSerializer.Serialize(settings))!;
    static int Number(JsonElement value, string name, int min, int max)
    {
        if (!value.GetProperty(name).TryGetInt32(out var number) || number < min || number > max)
            throw new ArgumentException("启动参数超出范围：" + name);
        return number;
    }
}
