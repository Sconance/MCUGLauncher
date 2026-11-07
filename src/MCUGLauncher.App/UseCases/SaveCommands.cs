using System.Text.Json;
using BlockLaunch;
using MCUGLauncher.Contracts;
using MCUGLauncher.Services;
using static MCUGLauncher.UseCases.ProtocolValues;

namespace MCUGLauncher.UseCases;

internal sealed class SaveCommands(LauncherContext context)
{
    string BackupRoot => Path.Combine(context.Store.DataDirectory, "backups");
    string Current => VersionCatalog.InstanceDirectory(context.Config.Root, VersionCatalog.Load(context.Config.Root, context.Config.LastInstance).Selected);
    public IReadOnlyList<SaveBackupInfo> List() =>
        SaveProtection.List(BackupRoot, Current)
            .Select(backup => new SaveBackupInfo(
                backup.Id,
                backup.Created.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                backup.Worlds.Select(world => world.World).ToArray()))
            .ToArray();

    public async Task<SavedInfo> BackupAsync()
    {
        var backup = await SaveProtection.Create(Current, BackupRoot, "手动备份", context.Config.LegacyPreferences.SaveBackupCount, context.Token);
        return new(backup == null ? "当前实例没有可备份的存档。" : "存档备份完成。");
    }

    public async Task<SavedInfo> RestoreAsync(JsonElement root)
    {
        var instance = Current;
        if (context.Games.UsesDirectory(instance))
            throw new IOException("请先关闭该实例的游戏再恢复存档。");
        var backup = SaveProtection.List(BackupRoot, instance).FirstOrDefault(b => b.Id == ReadString(root, "id")) ?? throw new IOException("备份不存在。");
        var item = backup.Worlds.FirstOrDefault(w => w.World == ReadString(root, "world")) ?? throw new IOException("备份中没有该存档。");
        var path = SaveProtection.ArchivePath(BackupRoot, instance, backup, item);
        await WorldArchives.Import(instance, path, item.World + "-恢复-" + DateTime.Now.ToString("yyyyMMddHHmmss"), token: context.Token);
        return new("已恢复为新存档，原存档未覆盖。");
    }
}
