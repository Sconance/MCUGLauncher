using System.Diagnostics;
using MCUGLauncher.Models;
using MCUGLauncher.Services;
using BlockLaunch;
using MCUGLauncher.Contracts;
using ModInfo = MCUGLauncher.Models.ModInfo;
using SavedInfo = MCUGLauncher.Contracts.SavedInfo;

namespace MCUGLauncher.UseCases;

/// <summary>实例目录访问与模组管理；明确目标的操作不修改当前选择。</summary>
/// <remarks>B 类。依赖 LauncherContext.Config/Store.DataDirectory/RefreshState，
/// VersionCatalog.Load/InstanceDirectory/ReadMods/SetEnabled/Delete，InstanceSnapshot.Capture(AppConfig,string,string)
/// 及 Models.ModInfo.File/Path；依赖面见 docs/components/GameDirectories.md，不依赖视图或其他 Commands。</remarks>
internal sealed class InstanceCommands(LauncherContext context)
{
    public void OpenFolder(string kind)
    {
        var catalog = VersionCatalog.Load(context.Config.Root, context.Config.LastInstance);
        var instance = VersionCatalog.InstanceDirectory(context.Config.Root, catalog.Selected);
        var path = kind switch
        {
            "mods" => Path.Combine(instance, "mods"),
            "saves" => Path.Combine(instance, "saves"),
            "screenshots" => Path.Combine(instance, "screenshots"),
            "version" => instance,
            "config" => context.Store.DataDirectory,
            _ => context.Config.Root
        };
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }

    public SavedInfo ToggleMod(string file, bool enabled, InstanceTarget? target = null)
    {
        var mod = FindMod(file, target);
        if (mod == null)
            throw new FileNotFoundException("找不到模组文件：" + file);
        VersionCatalog.SetEnabled(mod, enabled);
        context.RefreshState();
        return new(enabled ? "已启用模组。" : "已禁用模组。");
    }

    public SavedInfo DeleteMod(string file, InstanceTarget? target = null)
    {
        var mod = FindMod(file, target);
        if (mod == null)
            throw new FileNotFoundException("找不到模组文件：" + file);
        VersionCatalog.Delete(mod);
        context.RefreshState();
        return new("已删除模组。");
    }

    public IReadOnlyList<ModInfo> Mods(InstanceTarget target)
    {
        var instance = InstanceSnapshot.Capture(context.Config, target.Name, target.Root);
        return VersionCatalog.ReadMods(instance.Root, instance.Key);
    }

    public ModInfo? FindMod(string file, InstanceTarget? target = null)
    {
        if (target != null)
            return Mods(target).FirstOrDefault(item => item.File.Equals(file, StringComparison.OrdinalIgnoreCase));
        var catalog = VersionCatalog.Load(context.Config.Root, context.Config.LastInstance);
        return VersionCatalog.ReadMods(context.Config.Root, catalog.Selected)
            .FirstOrDefault(item => item.File.Equals(file, StringComparison.OrdinalIgnoreCase));
    }
}
