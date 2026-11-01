using System.Text.Json;
using BlockLaunch;
using MCUGLauncher.Services;
using static MCUGLauncher.UseCases.ProtocolValues;

namespace MCUGLauncher.UseCases;

internal sealed class SaveCommands(LauncherContext context)
{
    string BackupRoot => Path.Combine(context.Store.DataDirectory, "backups");
    string Current => VersionCatalog.InstanceDirectory(context.Config.Root, VersionCatalog.Load(context.Config.Root, context.Config.LastInstance).Selected);
    public void List()
    {
        var instance = Current;
        context.Send(new
        {
            type = "saveBackups",
            items = SaveProtection.List(BackupRoot, instance).Select(b => new { id = b.Id, created = b.Created.ToLocalTime().ToString("yyyy-MM-dd HH:mm"), worlds = b.Worlds.Select(w => w.World) })
        });
    }
    public async Task BackupAsync()
    {
        var backup = await SaveProtection.Create(Current, BackupRoot, "手动备份", context.Config.LegacyPreferences.SaveBackupCount, context.Token);
        context.Send(new
        {
            type = "toast",
            message = backup == null ? "当前实例没有可备份的存档。" : "存档备份完成。"
        });
        List();
    }
    public async Task RestoreAsync(JsonElement root)
    {
        var instance = Current;
        if (context.Games.UsesDirectory(instance))
            throw new IOException("请先关闭该实例的游戏再恢复存档。");
        var backup = SaveProtection.List(BackupRoot, instance).FirstOrDefault(b => b.Id == ReadString(root, "id")) ?? throw new IOException("备份不存在。");
        var item = backup.Worlds.FirstOrDefault(w => w.World == ReadString(root, "world")) ?? throw new IOException("备份中没有该存档。");
        var path = SaveProtection.ArchivePath(BackupRoot, instance, backup, item);
        await WorldArchives.Import(instance, path, item.World + "-恢复-" + DateTime.Now.ToString("yyyyMMddHHmmss"), token: context.Token);
        context.Send(new
        {
            type = "toast",
            message = "已恢复为新存档，原存档未覆盖。"
        });
    }
}
