using System.Diagnostics;
using MCUGLauncher.Models;
using MCUGLauncher.Services;

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
        VersionCatalog.SetEnabled(mod, enabled);
        context.RefreshState();
    }

    public void DeleteMod(string file)
    {
        var mod = FindMod(file);
        if (mod == null)
            return;
        VersionCatalog.Delete(mod);
        context.RefreshState();
    }

    public ModInfo? FindMod(string file)
    {
        var catalog = VersionCatalog.Load(context.Config.Root, context.Config.LastInstance);
        return VersionCatalog.ReadMods(context.Config.Root, catalog.Selected)
            .FirstOrDefault(item => item.File.Equals(file, StringComparison.OrdinalIgnoreCase));
    }
}
