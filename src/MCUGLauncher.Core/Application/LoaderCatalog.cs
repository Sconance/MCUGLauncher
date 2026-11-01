using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace BlockLaunch;

/// <summary>
/// 表示 LoaderRelease 数据。
/// </summary>
public record LoaderRelease(string Version, string Label)
{
    /// <summary>
    /// 执行 ToString。
    /// </summary>
    public override string ToString() => Label;
}

/// <summary>
/// 提供 LoaderCatalog 相关功能。
/// </summary>
public static class LoaderCatalog
{
    /// <summary>
    /// 获取。
    /// </summary>
    public static async Task<List<LoaderRelease>> Get(HttpClient http, string minecraft, string loader, CancellationToken token)
    {
        if (loader is not ("forge" or "neoforge"))
            throw new IOException("不支持的加载器。");
        var url = loader == "forge" ? "https://maven.minecraftforge.net/net/minecraftforge/forge/maven-metadata.xml" : "https://maven.neoforged.net/releases/net/neoforged/neoforge/maven-metadata.xml";
        var xml = XDocument.Parse(await http.GetStringAsync(url, token));
        return Parse(xml, minecraft, loader);
    }

    /// <summary>
    /// 解析。
    /// </summary>
    public static List<LoaderRelease> Parse(XDocument xml, string minecraft, string loader)
    {
        var mc = minecraft.Split('.');
        if (loader == "neoforge" && (mc.Length < 2 || mc[0] != "1" || !int.TryParse(mc[1], out var minor) || minor < 20 || minecraft is "1.20" or "1.20.1"))
            return [];
        var prefix = loader == "forge" ? minecraft + "-" : mc[1] + "." + (mc.Length > 2 ? mc[2] : "0") + ".";
        return xml.Descendants("version").Select(e => e.Value).Where(v => v.StartsWith(prefix, StringComparison.Ordinal)).Select(v => loader == "forge" ? v[prefix.Length..] : v).Where(v => Regex.IsMatch(v, @"^\d+(\.\d+)*(-beta)?$")).Distinct().OrderByDescending(v => Version.Parse(v.Replace("-beta", ""))).Select(v => new LoaderRelease(v, v + (v.EndsWith("-beta") ? " · 测试版" : ""))).ToList();
    }
}
