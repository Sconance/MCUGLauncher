using MCUGLauncher.Contracts;
using System.Text.Json;
using MCUGLauncher;
using MCUGLauncher.Services;
using static MCUGLauncher.UseCases.ProtocolValues;

namespace MCUGLauncher.UseCases;

/// <summary>读取、验证并保存全局或指定实例的启动选项。</summary>
/// <remarks>
/// B 类。成员级依赖见 docs/components/LaunchOptionsCommands.md。
/// LauncherContext.Config.Root/LegacyPreferences/AutoMemory/Ram/Java/Width/Height 提供当前配置；
/// Store.Save(AppConfig)/Send(object)/RefreshState() 持久化并回推；
/// InstanceSnapshot.Capture/GlobalSettings 冻结实例目标与全局选项，PreviewCache.Add/Get 绑定预览目标。
/// 不依赖窗口、视图或其他 Commands。
/// </remarks>
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
            settings = InstanceSnapshot.GlobalSettings(context.Config);
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
        var settings = InstanceSnapshot.GlobalSettings(context.Config);
        if (target.Version.Length > 0)
            settings = InstanceSnapshot.Capture(target.Root, target.Version, settings).Settings;
        settings.AutoMemory = options.GetProperty("autoMemory").GetBoolean();
        settings.FullScreen = options.GetProperty("fullScreen").GetBoolean();
        if (options.TryGetProperty("autoChinese", out var autoChinese))
            settings.AutoChinese = autoChinese.GetBoolean();
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
                throw new ArgumentException("实例显示名称需为 1–120 个字符。");
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

    static int Number(JsonElement value, string name, int min, int max)
    {
        if (!value.GetProperty(name).TryGetInt32(out var number) || number < min || number > max)
            throw new ArgumentException("启动参数超出范围：" + name);
        return number;
    }
}
