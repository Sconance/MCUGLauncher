using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MCUGLauncher.Services;

/// <summary>
/// 版本描述的判定规则，对齐 PCL2（Plain Craft Launcher 2）的 ModMinecraft.vb：
/// 版本号优先取 clientVersion / --fml.mcVersion / 继承链，最后才用正则从 id 与文件夹名里抠；
/// 类型按 releaseTime、JSON 的 type 字段和愚人节版本名单判定。
/// </summary>
internal static class VersionNaming
{
    public const string Release = "正式版";
    public const string Snapshot = "快照版";
    public const string PreRelease = "预发布版";
    public const string ReleaseCandidate = "发布候选";
    public const string Experimental = "实验性快照";
    public const string Fool = "愚人节版本";
    public const string Ancient = "远古版本";

    /// <summary>PCL2 的版本号正则：15w14a、1.20.1、1.21.11、1.20.1-pre3、24w14a、1.7.10 等。</summary>
    static readonly Regex VersionPattern = new(
        @"(([1-9][0-9]w[0-9]{2}[a-g])|((1|[2-9][0-9])\.[0-9]+(\.[0-9]+)?(-(pre|rc|snapshot-?)[1-9]*| Pre-Release( [1-9])?)?))(_unobfuscated)?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static string? MatchVersion(string text) =>
        VersionPattern.Match(text) is { Success: true } match ? match.Value : null;

    /// <summary>PCL2 的愚人节版本名单；命中即视为愚人节版本。</summary>
    public static bool IsFoolName(string version)
    {
        var name = version.ToLowerInvariant();
        return name.StartsWith("2.0", StringComparison.Ordinal)
               || name == "15w14a"
               || name == "1.rv-pre1"
               || name == "3d shareware v1.34"
               || name.StartsWith("20w14inf", StringComparison.Ordinal)
               || name == "20w14∞"
               || name == "22w13oneblockatatime"
               || name == "23w13a_or_b"
               || name == "24w14potato"
               || name == "25w14craftmine"
               || name == "26w14a";
    }

    /// <summary>PCL2 中 2000 &lt; 发布年份 &lt; 2013 的版本归为远古版本。</summary>
    public static bool IsAncient(DateTimeOffset? releaseTime) =>
        releaseTime is { Year: > 2000 and < 2013 };

    public static DateTimeOffset? ReleaseTime(JsonElement json)
    {
        if (!json.TryGetProperty("releaseTime", out var value) || value.ValueKind != JsonValueKind.String)
            return null;
        return DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out var time)
            ? time
            : null;
    }

    /// <summary>按 PCL2 的 VersionDisplayName 分支顺序判定类型文案。</summary>
    public static string Classify(JsonElement json, DateTimeOffset? releaseTime, string folderName, string version)
    {
        var type = String(json, "type");
        if (type.Equals("fool", StringComparison.OrdinalIgnoreCase)
            || IsFoolName(version)
            || (type.Equals("snapshot", StringComparison.OrdinalIgnoreCase)
                && releaseTime is { } published
                && published.ToUniversalTime().AddHours(2) is { Month: 4, Day: 1 }))
            return Fool;
        if (version.Contains("pre", StringComparison.OrdinalIgnoreCase))
            return PreRelease;
        if (version.Contains("rc", StringComparison.OrdinalIgnoreCase))
            return ReleaseCandidate;
        if (version.Contains("experimental", StringComparison.OrdinalIgnoreCase))
            return Experimental;
        return IsSnapshot(type, folderName, version) ? Snapshot : Release;
    }

    /// <summary>PCL2 的 IsSnapshot()：版本名特征、combat 版本名，或 JSON 的 type 标记。</summary>
    static bool IsSnapshot(string type, string folderName, string version) =>
        Contains(version, "w")
        || Contains(version, "snapshot")
        || Contains(version, "rc")
        || Contains(version, "pre")
        || Contains(version, "experimental")
        || version.Contains('-')
        || Contains(folderName, "combat")
        || type.Equals("snapshot", StringComparison.OrdinalIgnoreCase)
        || type.Equals("pending", StringComparison.OrdinalIgnoreCase)
        || type.Equals("fool", StringComparison.OrdinalIgnoreCase);

    static bool Contains(string text, string value) =>
        text.Contains(value, StringComparison.OrdinalIgnoreCase);

    internal static string String(JsonElement json, string name) =>
        json.ValueKind == JsonValueKind.Object
        && json.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
}
