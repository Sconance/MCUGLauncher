using System.Reflection;
using BlockLaunch;

namespace MCUGLauncher.Ui;

/// <summary>
/// 内嵌 Ui 资源的解压缓存。
/// </summary>
/// <remarks>
/// 依赖 <see cref="System.Reflection.Assembly"/> 清单资源、<c>ProductInfo.Version</c> 与 <c>%LOCALAPPDATA%</c>；可在任意线程调用，内部只做文件 IO。
/// </remarks>
static class EmbeddedUiCache
{
    const string ResourcePrefix = "MCUGLauncher.Ui.";

    /// <summary>
    /// 解压内嵌资源到按版本隔离的目录。
    /// </summary>
    /// <returns>解压后的缓存目录绝对路径</returns>
    /// <remarks>
    /// 目标为 %LOCALAPPDATA%/MCUGLauncher/ui-&lt;版本&gt;，每次启动覆盖写入，保证缓存与程序集一致；资源名中的点会还原为目录结构（assets/logo.png）。
    /// </remarks>
    public static string Extract() => ExtractTo(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MCUGLauncher", "ui-" + ProductInfo.Version));

    // Explicit destination keeps integration checks isolated from a user's running launcher cache.
    internal static string ExtractTo(string target)
    {
        Directory.CreateDirectory(target);

        var assembly = Assembly.GetExecutingAssembly();
        foreach (var name in assembly.GetManifestResourceNames().Where(name => name.StartsWith(ResourcePrefix)))
        {
            var file = Path.Combine(target, name[ResourcePrefix.Length..]);
            // 内嵌资源名会把目录分隔符压成点，assets/logo.png 需要还原目录结构。
            if (file.EndsWith("assets.logo.png", StringComparison.OrdinalIgnoreCase))
                file = Path.Combine(target, "assets", "logo.png");

            var folder = Path.GetDirectoryName(file);
            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);

            using var source = assembly.GetManifestResourceStream(name);
            if (source == null)
                continue;
            using var destination = File.Create(file);
            source.CopyTo(destination);
        }
        return target;
    }
}
