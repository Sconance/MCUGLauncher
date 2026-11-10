namespace BlockLaunch.Downloads;

/// <summary>
/// 把底层 IO 与网络异常翻译成可展示的中文提示。
/// </summary>
public static class DownloadErrors
{
    /// <summary>
    /// 判断异常是否为不可重试的本地文件错误（磁盘满、文件被占用、只读等）。
    /// </summary>
    public static bool IsPermanentFileError(Exception ex) => ex is IOException && (ex.HResult & 0xffff) is 3 or 5 or 19 or 32 or 33 or 39 or 112 or 206 or 267;

    /// <summary>
    /// 把异常翻译成带处理建议的 <see cref="IOException"/>。
    /// </summary>
    public static IOException Translate(Exception ex)
    {
        if (ex is UnauthorizedAccessException)
            return new IOException("下载目录或文件无法写入，请检查目录权限或只读属性；已有文件与下载进度已保留。", ex);
        if (ex is IOException && IsPermanentFileError(ex))
        {
            var code = ex.HResult & 0xffff;
            var reason = code is 39 or 112 ? "磁盘空间不足，请释放空间后重试" : code is 32 or 33 ? "文件正在被其他程序占用，请关闭占用程序后重试" : "下载目录不可用，请检查路径、权限或只读属性";
            return new IOException(reason + "；已有文件与下载进度已保留。", ex);
        }
        return ex as IOException ?? new IOException(ex.Message, ex);
    }

    /// <summary>
    /// 把 HTTP 状态码翻译成可操作的中文提示。
    /// </summary>
    public static string StatusMessage(System.Net.HttpStatusCode status) => (int)status switch
    {
        400 => "服务器无法处理该请求（400），请检查下载地址。",
        401 or 403 => $"服务器拒绝访问（{(int)status}）：该网站可能启用了人机验证或防盗链，启动器无法直接下载；请用浏览器下载后再导入。",
        404 or 410 => $"文件不存在或已删除（{(int)status}），请检查下载地址。",
        408 => "服务器响应超时（408），请稍后重试。",
        429 => "请求过于频繁（429），请稍后重试。",
        >= 500 => $"服务器暂时不可用（{(int)status}），请稍后重试。",
        _ => $"服务器返回错误（{(int)status}）。"
    };

    /// <summary>
    /// 判断异常是否值得换源或重试。
    /// </summary>
    public static bool IsTransient(Exception ex) => ex switch
    {
        OperationCanceledException => true,
        HttpRequestException http => http.StatusCode is null or System.Net.HttpStatusCode.RequestTimeout or System.Net.HttpStatusCode.TooManyRequests || (int?)http.StatusCode >= 500,
        IOException => !IsPermanentFileError(ex),
        _ => false
    };
}
