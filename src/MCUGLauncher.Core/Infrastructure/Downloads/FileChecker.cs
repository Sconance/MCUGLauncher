using System.Security.Cryptography;

namespace BlockLaunch.Downloads;

/// <summary>
/// 本地文件的哈希与大小校验。
/// </summary>
/// <remarks>
/// 只做只读打开与流式哈希，不修改文件；被其他进程独占的文件会抛出共享冲突，由调用方转成友好提示。
/// </remarks>
public static class FileChecker
{
    /// <summary>
    /// 返回算法对应的十六进制摘要长度。
    /// </summary>
    public static int HexLength(HashAlgorithmName algorithm) => algorithm.Name switch
    {
        "SHA1" => 40,
        "SHA256" => 64,
        "SHA512" => 128,
        _ => throw new IOException("不支持的哈希算法：" + algorithm.Name)
    };

    /// <summary>
    /// 把配置中的算法名转成 <see cref="HashAlgorithmName"/>。
    /// </summary>
    public static HashAlgorithmName ParseAlgorithm(string? name, HashAlgorithmName fallback) => name?.Trim().ToUpperInvariant() switch
    {
        "SHA1" or "SHA-1" => HashAlgorithmName.SHA1,
        "SHA256" or "SHA-256" => HashAlgorithmName.SHA256,
        "SHA512" or "SHA-512" => HashAlgorithmName.SHA512,
        _ => fallback
    };

    /// <summary>
    /// 判断文本是否为指定算法的合法十六进制摘要。
    /// </summary>
    public static bool IsValidHash(string? hash, HashAlgorithmName algorithm) => hash != null && hash.Length == HexLength(algorithm) && hash.All(Uri.IsHexDigit);

    /// <summary>
    /// 计算文件的十六进制哈希。
    /// </summary>
    public static async Task<string> ComputeAsync(string path, HashAlgorithmName algorithm, CancellationToken token)
    {
        await using var input = File.OpenRead(path);
        var actual = algorithm.Name switch
        {
            "SHA1" => await SHA1.HashDataAsync(input, token),
            "SHA256" => await SHA256.HashDataAsync(input, token),
            "SHA512" => await SHA512.HashDataAsync(input, token),
            _ => throw new IOException("不支持的哈希算法：" + algorithm.Name)
        };
        return Convert.ToHexString(actual);
    }

    /// <summary>
    /// 判断本地文件是否已满足期望哈希与大小。
    /// </summary>
    public static async Task<bool> MatchesAsync(string path, string? hash, HashAlgorithmName algorithm, long size, long maxBytes, CancellationToken token)
    {
        if (!File.Exists(path))
            return false;
        var length = new FileInfo(path).Length;
        if (length > maxBytes || size > 0 && length != size)
            return false;
        if (string.IsNullOrEmpty(hash))
            return size > 0;
        if (!IsValidHash(hash, algorithm))
            return false;
        var actual = await ComputeAsync(path, algorithm, token);
        return actual.Equals(hash, StringComparison.OrdinalIgnoreCase);
    }
}
