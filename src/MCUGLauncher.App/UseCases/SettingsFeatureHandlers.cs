using System.Text.Json;
using MCUGLauncher.Contracts;

namespace MCUGLauncher.UseCases;

[FeatureHandler(nameof(ILauncherService.ScanJavaAsync))]
internal sealed class ScanJavaFeatureHandler(LauncherContext context)
    : IFeatureHandler<IReadOnlyList<JavaInfo>>
{
    public Task<IReadOnlyList<JavaInfo>> HandleAsync(JsonElement arguments, CancellationToken token) =>
        new SettingsCommands(context).ScanJavaAsync();
}

[FeatureHandler(nameof(ILauncherService.ReadLogAsync))]
internal sealed class ReadLogFeatureHandler(LauncherContext context)
    : IFeatureHandler<string>
{
    public Task<string> HandleAsync(JsonElement arguments, CancellationToken token) =>
        Task.FromResult(new DiagnosticCommands(context).ReadLog());
}

[FeatureHandler(nameof(ILauncherService.SystemInfoAsync))]
internal sealed class SystemInfoFeatureHandler(LauncherContext context)
    : IFeatureHandler<SystemInfoInfo>
{
    public Task<SystemInfoInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        Task.FromResult(new DiagnosticCommands(context).SendSystemInfo());
}

[FeatureHandler(nameof(ILauncherService.SaveSettingsAsync))]
internal sealed class SaveSettingsFeatureHandler(LauncherContext context)
    : IFeatureHandler<SavedInfo>
{
    public Task<SavedInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        Task.FromResult(new SettingsCommands(context).SaveSettings(arguments));
}

[FeatureHandler(nameof(ILauncherService.SaveCustomSourcesAsync))]
internal sealed class SaveCustomSourcesFeatureHandler(LauncherContext context)
    : IFeatureHandler<SavedInfo>
{
    public Task<SavedInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        Task.FromResult(new SettingsCommands(context).SaveCustomSources(arguments));
}

[FeatureHandler(nameof(ILauncherService.UseDataSourcesAsync))]
internal sealed class UseDataSourcesFeatureHandler(LauncherContext context)
    : IFeatureHandler<SavedInfo>
{
    public Task<SavedInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        Task.FromResult(new SettingsCommands(context).UseDataSources(
            ProtocolValues.ReadString(arguments, "mode")));
}

[FeatureHandler(nameof(ILauncherService.ResetDataSourcesAsync))]
internal sealed class ResetDataSourcesFeatureHandler(LauncherContext context)
    : IFeatureHandler<SavedInfo>
{
    public Task<SavedInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        Task.FromResult(new SettingsCommands(context).ResetDataSources());
}

[FeatureHandler(nameof(ILauncherService.BackupSavesAsync))]
internal sealed class BackupSavesFeatureHandler(LauncherContext context)
    : IFeatureHandler<SavedInfo>
{
    public Task<SavedInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        new SaveCommands(context).BackupAsync();
}

[FeatureHandler(nameof(ILauncherService.ListSaveBackupsAsync))]
internal sealed class ListSaveBackupsFeatureHandler(LauncherContext context)
    : IFeatureHandler<IReadOnlyList<SaveBackupInfo>>
{
    public Task<IReadOnlyList<SaveBackupInfo>> HandleAsync(JsonElement arguments, CancellationToken token) =>
        Task.FromResult(new SaveCommands(context).List());
}

[FeatureHandler(nameof(ILauncherService.RestoreSaveAsync))]
internal sealed class RestoreSaveFeatureHandler(LauncherContext context)
    : IFeatureHandler<SavedInfo>
{
    public Task<SavedInfo> HandleAsync(JsonElement arguments, CancellationToken token)
    {
        var command = new SaveCommands(context);
        return command.RestoreAsync(JsonSerializer.SerializeToElement(new
        {
            id = ProtocolValues.ReadString(arguments, "id"),
            world = ProtocolValues.ReadString(arguments, "world")
        }));
    }
}
