namespace BlockLaunch.Downloads;

/// <summary>
/// 源不支持分片时抛出，调用方应降级为单流下载。
/// </summary>
internal sealed class RangeNotSupportedException(string message) : IOException(message);

/// <summary>
/// 完整文件哈希校验失败时抛出，调用方应清理临时文件后重试。
/// </summary>
internal sealed class HashMismatchException(string message) : IOException(message);
