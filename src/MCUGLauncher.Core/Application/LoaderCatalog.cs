using System.Text.RegularExpressions;
using System.Text.Json;
using System.Xml.Linq;

namespace MCUGLauncher;

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
/// 查询提供方声明兼容的加载器版本；统一处理目录解析、网络故障和取消。
/// </summary>
public static class LoaderCatalog
{
    /// <summary>
    /// 查询指定游戏与加载器的版本；不支持的游戏返回空列表，服务故障抛出可读异常。
    /// </summary>
    public static async Task<List<LoaderRelease>> Get(HttpClient http, string minecraft, string loader, CancellationToken token)
    {
        try
        {
            if (loader is "fabric" or "quilt")
                return await MetaVersions(http, minecraft, loader, token);
            if (loader is not ("forge" or "neoforge"))
                throw new IOException("不支持的加载器。");
            var url = loader == "forge" ? "https://maven.minecraftforge.net/net/minecraftforge/forge/maven-metadata.xml" : "https://maven.neoforged.net/releases/net/neoforged/neoforge/maven-metadata.xml";
            var xml = XDocument.Parse(await http.GetStringAsync(url, token));
            return Parse(xml, minecraft, loader);
        }
        catch (HttpRequestException error)
        {
            var status = error.StatusCode is { } code ? "（HTTP " + (int)code + "）" : "";
            throw new IOException("读取加载器列表失败" + status + "，请检查网络或稍后重试。", error);
        }
        catch (OperationCanceledException error) when (!token.IsCancellationRequested)
        {
            throw new IOException("读取加载器列表超时，请检查网络后重试。", error);
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("加载器服务返回的数据格式无效，请稍后重试。", error);
        }
    }

    // Use the provider's game catalog rather than a hard-coded minimum; snapshots and future versions differ.
    static async Task<List<LoaderRelease>> MetaVersions(HttpClient http, string minecraft, string loader, CancellationToken token)
    {
        var endpoint = loader == "fabric" ? "https://meta.fabricmc.net/v2" : "https://meta.quiltmc.org/v3";
        using var games = JsonDocument.Parse(await http.GetStringAsync(endpoint + "/versions/game", token));
        if (!games.RootElement.EnumerateArray().Any(v => v.GetProperty("version").GetString() == minecraft))
            return [];
        using var releases = JsonDocument.Parse(await http.GetStringAsync(endpoint + "/versions/loader/" + Uri.EscapeDataString(minecraft), token));
        return releases.RootElement.EnumerateArray()
            .Select(v => v.GetProperty("loader").GetProperty("version").GetString())
            .Where(v => !string.IsNullOrWhiteSpace(v)).Distinct()
            .Select(v => new LoaderRelease(v!, v!)).ToList();
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
