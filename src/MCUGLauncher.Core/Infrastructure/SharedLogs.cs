using System.Text;
using System.Text.RegularExpressions;

namespace BlockLaunch;

/// <summary>
/// 提供 日志Privacy 相关功能。
/// </summary>
public static class LogPrivacy
{
    /// <summary>
    /// 执行 Redact。
    /// </summary>
    public static string Redact(string text, IEnumerable<string>? secrets = null)
    {
        if (secrets != null)
            foreach (var secret in secrets.Where(s => s.Length > 4))
                text = text.Replace(secret, "[已隐藏]", StringComparison.Ordinal);
        text = Regex.Replace(text, @"(?i)\bBearer\s+[A-Za-z0-9._~+/=-]+", "Bearer [已隐藏]", RegexOptions.None, TimeSpan.FromMilliseconds(250));
        text = Regex.Replace(text, @"(?i)((?:access[_-]?token|refresh[_-]?token|authorization|x-api-key|api[_-]?key|password|client_secret)[""']?\s*[:=]\s*)(?:""[^""\r\n]*""|'[^'\r\n]*')", "$1[已隐藏]", RegexOptions.None, TimeSpan.FromMilliseconds(250));
        text = Regex.Replace(text, "(?i)((?:access[_-]?token|refresh[_-]?token|authorization|x-api-key|api[_-]?key|password|client_secret)\\s*[\\\"']?\\s*[:=]\\s*[\\\"']?)([^\\\"'\\s,;}&]+)", "$1[已隐藏]");
        return Regex.Replace(text, @"(?i)(--(?:accessToken|clientToken)\s+)([^\s]+)", "$1[已隐藏]");
    }
}

/// <summary>
/// 提供 SharedLogs 相关功能。
/// </summary>
public static class SharedLogs
{
    /// <summary>
    /// 读取Tail。
    /// </summary>
    public static string ReadTail(string file, int maxBytes = 65536)
    {
        if (!File.Exists(file))
            return "日志尚未生成。";
        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var offset = Math.Max(0, stream.Length - maxBytes);
        stream.Seek(offset, SeekOrigin.Begin);
        using var reader = new StreamReader(stream, Encoding.UTF8, true);
        if (offset > 0)
            reader.ReadLine();
        return LogPrivacy.Redact(reader.ReadToEnd());
    }
}
