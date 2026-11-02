using System.Diagnostics;
using MCUGLauncher.Models;
using MCUGLauncher.Services;

namespace MCUGLauncher.UseCases;

/// <summary>打开实例目录并修改选中实例的本地模组。</summary>
/// <remarks>
/// B 类，完整依赖见 docs/components/InstanceCommands.md。
/// LauncherContext.Config.Root/LastInstance 读取目标；IsInstanceInUse(string) 阻止修改活动实例；
/// RefreshState() 回推结果。VersionCatalog.Load/InstanceDirectory/ReadMods/SetEnabled/Delete 适配本地列表与 Core 内容操作。
/// ModInfo.File/Path 用于定位已枚举文件；不接受任意路径，不依赖窗口或其他 Commands。
/// </remarks>
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
            _ => context.Config.Root
        };
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }

    public void ToggleMod(string file, bool enabled)
    {
        var mod = FindMod(file);
        if (mod == null)
            return;
        RequireStopped(mod);
        VersionCatalog.SetEnabled(mod, enabled);
        context.RefreshState();
    }

    public void DeleteMod(string file)
    {
        var mod = FindMod(file);
        if (mod == null)
            return;
        RequireStopped(mod);
        VersionCatalog.Delete(mod);
        context.RefreshState();
    }

    void RequireStopped(ModInfo mod)
    {
        var directory = Directory.GetParent(Path.GetDirectoryName(mod.Path)!)!.FullName;
        if (context.IsInstanceInUse(directory))
            throw new IOException("请先关闭该实例的游戏，或等待启动准备完成，再修改模组。");
    }

    public ModInfo? FindMod(string file)
    {
        var catalog = VersionCatalog.Load(context.Config.Root, context.Config.LastInstance);
        return VersionCatalog.ReadMods(context.Config.Root, catalog.Selected)
            .FirstOrDefault(item => item.File.Equals(file, StringComparison.OrdinalIgnoreCase));
    }
}
