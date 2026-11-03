using System.Diagnostics;
using MCUGLauncher.Models;
using MCUGLauncher.Services;
using SavedInfo = MCUGLauncher.Contracts.SavedInfo;

namespace MCUGLauncher.UseCases;

// Handles instance use cases; no WPF controls or browser access.
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

    public SavedInfo ToggleMod(string file, bool enabled)
    {
        var mod = FindMod(file);
        if (mod == null)
            throw new FileNotFoundException("找不到模组文件：" + file);
        VersionCatalog.SetEnabled(mod, enabled);
        context.RefreshState();
        return new(enabled ? "已启用模组。" : "已禁用模组。");
    }

    public SavedInfo DeleteMod(string file)
    {
        var mod = FindMod(file);
        if (mod == null)
            throw new FileNotFoundException("找不到模组文件：" + file);
        VersionCatalog.Delete(mod);
        context.RefreshState();
        return new("已删除模组。");
    }

    public ModInfo? FindMod(string file)
    {
        var catalog = VersionCatalog.Load(context.Config.Root, context.Config.LastInstance);
        return VersionCatalog.ReadMods(context.Config.Root, catalog.Selected)
            .FirstOrDefault(item => item.File.Equals(file, StringComparison.OrdinalIgnoreCase));
    }
}
