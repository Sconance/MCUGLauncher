using System.Reflection;

namespace BlockLaunch;

/// <summary>
/// 产品名称与运行期版本号的唯一入口。
/// 版本号来自 <c>Directory.Build.props</c> 的 <c>McugVersion</c>，经程序集元数据写入，
/// 因此界面、User-Agent 和诊断信息不再各自维护字面量。
/// </summary>
public static class ProductInfo
{
    public const string Name = "MCUG Launcher";

    /// <summary>
    /// 获取或设置 版本。
    /// </summary>
    public static string Version { get; } = ReadVersion();

    static string ReadVersion()
    {
        var informational = typeof(ProductInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var value = informational?.Split('+')[0].Trim();
        if (!string.IsNullOrEmpty(value))
            return value;

        var assembly = typeof(ProductInfo).Assembly.GetName().Version;
        return assembly is null ? "0.0.0" : $"{assembly.Major}.{assembly.Minor}.{assembly.Build}";
    }
}
