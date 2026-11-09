using System.Text.Json;
using MCUGLauncher.Contracts;
using MCUGLauncher.Models;
using MCUGLauncher.Services;
using static MCUGLauncher.UseCases.ProtocolValues;
using ModInfo = MCUGLauncher.Contracts.ModInfo;

namespace MCUGLauncher.UseCases;

/// <summary>类型化目录与跨目录实例操作的入口，不改变当前目录来执行后台操作。</summary>
/// <remarks>B 类。读取 LauncherContext.Config.Root/GameRoots，构造 AppConfig 的目录快照，
/// 调用 GameDirectoryCatalog.Scan(AppConfig,CancellationToken)。不依赖窗口、控件、网络或内部命令桥。
/// 其余适配器的逐成员依赖见 docs/components/GameDirectories.md。</remarks>
[FeatureHandler(nameof(ILauncherService.ScanGameDirectoriesAsync))]
internal sealed class ScanGameDirectoriesFeatureHandler(LauncherContext context) : IFeatureHandler<IReadOnlyList<GameDirectoryInfo>>
{
    public Task<IReadOnlyList<GameDirectoryInfo>> HandleAsync(JsonElement arguments, CancellationToken token)
    {
        var snapshot = new AppConfig { Root = context.Config.Root, GameRoots = [.. context.Config.GameRoots] };
        return Task.Run(() => GameDirectoryCatalog.Scan(snapshot, token), token);
    }
}

/// <summary>AddGameDirectoryAsync 的类型化适配。</summary>
/// <remarks>B 类。通过构造参数传递 LauncherContext，依赖 SettingsCommands.AddGameDirectory(string,bool)、ProtocolValues.ReadString(JsonElement,string)；
/// 不读取配置或控件，不新增业务规则。详见 docs/components/GameDirectories.md。</remarks>
[FeatureHandler(nameof(ILauncherService.AddGameDirectoryAsync))]
internal sealed class AddGameDirectoryFeatureHandler(LauncherContext context) : IFeatureHandler<SavedInfo>
{
    public Task<SavedInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        Task.FromResult(new SettingsCommands(context).AddGameDirectory(ReadString(arguments, "root"), arguments.GetProperty("select").GetBoolean()));
}

/// <summary>RemoveGameDirectoryAsync 的类型化适配。</summary>
/// <remarks>B 类。通过构造参数传递 LauncherContext，依赖 SettingsCommands.RemoveGameDirectory(string)、ProtocolValues.ReadString(JsonElement,string)；
/// 不读取配置或控件，不新增业务规则。详见 docs/components/GameDirectories.md。</remarks>
[FeatureHandler(nameof(ILauncherService.RemoveGameDirectoryAsync))]
internal sealed class RemoveGameDirectoryFeatureHandler(LauncherContext context) : IFeatureHandler<SavedInfo>
{
    public Task<SavedInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        Task.FromResult(new SettingsCommands(context).RemoveGameDirectory(ReadString(arguments, "root")));
}

/// <summary>SelectManagedInstanceAsync 的类型化适配。</summary>
/// <remarks>B 类。通过构造参数传递 LauncherContext，依赖 SettingsCommands.SelectManagedInstance(InstanceTarget)、ProtocolValues.ReadInstanceTarget(JsonElement)；
/// 不读取配置或控件，不新增业务规则。详见 docs/components/GameDirectories.md。</remarks>
[FeatureHandler(nameof(ILauncherService.SelectManagedInstanceAsync))]
internal sealed class SelectManagedInstanceFeatureHandler(LauncherContext context) : IFeatureHandler<SavedInfo>
{
    public Task<SavedInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        Task.FromResult(new SettingsCommands(context).SelectManagedInstance(ReadInstanceTarget(arguments)));
}

/// <summary>LaunchManagedInstanceAsync 的类型化适配。</summary>
/// <remarks>B 类。通过构造参数传递 LauncherContext，依赖 GameCommands.LaunchManagedAsync(InstanceTarget,string)、ProtocolValues.ReadInstanceTarget(JsonElement)；
/// 不读取配置或控件，不新增业务规则。详见 docs/components/GameDirectories.md。</remarks>
[FeatureHandler(nameof(ILauncherService.LaunchManagedInstanceAsync))]
internal sealed class LaunchManagedInstanceFeatureHandler(LauncherContext context) : IFeatureHandler<LaunchResultInfo>
{
    public Task<LaunchResultInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        new GameCommands(context).LaunchManagedAsync(ReadInstanceTarget(arguments),
            arguments.TryGetProperty("serverAddress", out var server) && server.ValueKind == JsonValueKind.String ? server.GetString() : null);
}

/// <summary>CloneManagedInstanceAsync 的类型化适配。</summary>
/// <remarks>B 类。通过构造参数传递 LauncherContext，依赖 InstanceMaintenanceCommands.Clone(JsonElement,InstanceTarget)、ProtocolValues.ReadInstanceTarget(JsonElement)；
/// 不读取配置或控件，不新增业务规则。详见 docs/components/GameDirectories.md。</remarks>
[FeatureHandler(nameof(ILauncherService.CloneManagedInstanceAsync))]
internal sealed class CloneManagedInstanceFeatureHandler(LauncherContext context) : IFeatureHandler<SavedInfo>
{
    public Task<SavedInfo> HandleAsync(JsonElement arguments, CancellationToken token)
    {
        new InstanceMaintenanceCommands(context).Clone(arguments, ReadInstanceTarget(arguments));
        return Task.FromResult(new SavedInfo("克隆任务已入队。"));
    }
}

/// <summary>ExportManagedInstanceAsync 的类型化适配。</summary>
/// <remarks>B 类。通过构造参数传递 LauncherContext，依赖 InstanceMaintenanceCommands.Export(JsonElement,InstanceTarget)、ProtocolValues.ReadInstanceTarget(JsonElement)；
/// 不读取配置或控件，不新增业务规则。详见 docs/components/GameDirectories.md。</remarks>
[FeatureHandler(nameof(ILauncherService.ExportManagedInstanceAsync))]
internal sealed class ExportManagedInstanceFeatureHandler(LauncherContext context) : IFeatureHandler<SavedInfo>
{
    public Task<SavedInfo> HandleAsync(JsonElement arguments, CancellationToken token)
    {
        new InstanceMaintenanceCommands(context).Export(arguments, ReadInstanceTarget(arguments));
        return Task.FromResult(new SavedInfo("导出任务已入队。"));
    }
}

/// <summary>PreviewManagedUninstallAsync 的类型化适配。</summary>
/// <remarks>B 类。通过构造参数传递 LauncherContext，依赖 UninstallCommands.PreviewAsync(string,string)、ProtocolValues.ReadInstanceTarget(JsonElement)；
/// 不读取配置或控件，不新增业务规则。详见 docs/components/GameDirectories.md。</remarks>
[FeatureHandler(nameof(ILauncherService.PreviewManagedUninstallAsync))]
internal sealed class PreviewManagedUninstallFeatureHandler(LauncherContext context) : IFeatureHandler<UninstallPreviewInfo>
{
    public Task<UninstallPreviewInfo> HandleAsync(JsonElement arguments, CancellationToken token)
    {
        var target = ReadInstanceTarget(arguments);
        return new UninstallCommands(context).PreviewAsync(target.Name, target.Root);
    }
}

/// <summary>ListManagedModsAsync 的类型化适配。</summary>
/// <remarks>B 类。通过构造参数传递 LauncherContext，依赖 InstanceCommands.Mods(InstanceTarget)、ProtocolValues.ReadInstanceTarget(JsonElement) 和 ModInfo 构造函数；
/// 不读取配置或控件，不新增业务规则。详见 docs/components/GameDirectories.md。</remarks>
[FeatureHandler(nameof(ILauncherService.ListManagedModsAsync))]
internal sealed class ListManagedModsFeatureHandler(LauncherContext context) : IFeatureHandler<IReadOnlyList<ModInfo>>
{
    public Task<IReadOnlyList<ModInfo>> HandleAsync(JsonElement arguments, CancellationToken token) =>
        Task.FromResult<IReadOnlyList<ModInfo>>(new InstanceCommands(context).Mods(ReadInstanceTarget(arguments))
            .Select(item => new ModInfo(item.Id, item.Name, item.File, item.Version, item.Enabled, item.Path, item.Size)).ToArray());
}

/// <summary>ToggleManagedModAsync 的类型化适配。</summary>
/// <remarks>B 类。通过构造参数传递 LauncherContext，依赖 InstanceCommands.ToggleMod(string,bool,InstanceTarget)、ProtocolValues.ReadString/ReadInstanceTarget(JsonElement)；
/// 不读取配置或控件，不新增业务规则。详见 docs/components/GameDirectories.md。</remarks>
[FeatureHandler(nameof(ILauncherService.ToggleManagedModAsync))]
internal sealed class ToggleManagedModFeatureHandler(LauncherContext context) : IFeatureHandler<SavedInfo>
{
    public Task<SavedInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        Task.FromResult(new InstanceCommands(context).ToggleMod(ReadString(arguments, "file"), arguments.GetProperty("enabled").GetBoolean(), ReadInstanceTarget(arguments)));
}

/// <summary>DeleteManagedModAsync 的类型化适配。</summary>
/// <remarks>B 类。通过构造参数传递 LauncherContext，依赖 InstanceCommands.DeleteMod(string,InstanceTarget)、ProtocolValues.ReadString/ReadInstanceTarget(JsonElement)；
/// 不读取配置或控件，不新增业务规则。详见 docs/components/GameDirectories.md。</remarks>
[FeatureHandler(nameof(ILauncherService.DeleteManagedModAsync))]
internal sealed class DeleteManagedModFeatureHandler(LauncherContext context) : IFeatureHandler<SavedInfo>
{
    public Task<SavedInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        Task.FromResult(new InstanceCommands(context).DeleteMod(ReadString(arguments, "file"), ReadInstanceTarget(arguments)));
}
