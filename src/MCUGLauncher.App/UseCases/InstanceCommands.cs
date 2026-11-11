using System.Diagnostics;
using MCUGLauncher.Models;
using MCUGLauncher.Services;
using ContractModInfo = MCUGLauncher.Contracts.ModInfo;
using SavedInfo = MCUGLauncher.Contracts.SavedInfo;

namespace MCUGLauncher.UseCases;

/// <summary>实例选择、目录与模组维护用例。</summary>
/// <remarks>
/// 组件级别：B。
/// 依赖：
/// - LauncherContext.Config：读取根目录/当前实例，保存最近选择；
/// - LauncherContext.Store.Save(AppConfig)：持久化实例/版本选择；
/// - LauncherContext.Store.DataDirectory：打开配置目录；
/// - LauncherContext.RefreshState：选择变化后刷新状态；
/// - VersionCatalog.Load/InstanceDirectory/ReadMods/SetEnabled/Delete：实例与模组目录操作；
/// - SavedInfo/ModInfo：返回维护结果。
/// 不依赖：原生窗口/浏览器适配、LauncherService、LauncherApplication。
/// </remarks>
internal sealed class InstanceCommands(LauncherContext context)
{
    public void SelectVersion(string version)
    {
        context.Config.LastVersion = version;
        context.Config.LastInstance = version;
        context.Store.Save(context.Config);
        context.RefreshState();
    }

    public void SelectInstance(string instance)
    {
        context.Config.LastInstance = instance;
        context.Store.Save(context.Config);
        context.RefreshState();
    }

    public void OpenFolder(string kind, string instance = "")
    {
        // 指定实例时按该实例解析目录；留空仍跟随当前实例
        var name = string.IsNullOrWhiteSpace(instance) ? context.Config.LastInstance : instance;
        var catalog = VersionCatalog.Load(context.Config.Root, name);
        var instancePath = VersionCatalog.InstanceDirectory(context.Config.Root, catalog.Selected);
        var path = kind switch
        {
            "mods" => Path.Combine(instancePath, "mods"),
            "saves" => Path.Combine(instancePath, "saves"),
            "screenshots" => Path.Combine(instancePath, "screenshots"),
            "version" => instancePath,
            "config" => context.Store.DataDirectory,
            _ => context.Config.Root
        };
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }

    /// <summary>按实例名读取模组，不改变当前启动实例。</summary>
    public IReadOnlyList<ContractModInfo> ListMods(string instance)
    {
        var name = string.IsNullOrWhiteSpace(instance) ? context.Config.LastInstance : instance;
        var catalog = VersionCatalog.Load(context.Config.Root, name);
        return VersionCatalog.ReadMods(context.Config.Root, catalog.Selected)
            .Select(item => new ContractModInfo(
                item.Id,
                item.Name,
                item.File,
                item.Version,
                item.Enabled,
                item.Path,
                item.Size))
            .ToArray();
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
