using System.Text.Json;
using BlockLaunch;
using MCUGLauncher.Contracts;
using static MCUGLauncher.UseCases.ProtocolValues;

namespace MCUGLauncher.UseCases;

// Validates a custom file download request and hands it to the persisted task queue.
internal sealed class CustomDownloadCommands(LauncherContext context)
{
    private static readonly string[] ReservedNames =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    ];

    public CustomDownloadInfo Start(JsonElement root)
    {
        var url = ReadString(root, "url").Trim();
        var directory = ReadString(root, "directory").Trim();
        var fileName = ReadString(root, "fileName").Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("下载地址格式不正确，仅支持 http 或 https。");
        }

        if (directory.Length == 0)
        {
            throw new ArgumentException("请选择保存文件夹。");
        }

        var folder = Path.GetFullPath(directory);
        if (!Directory.Exists(folder))
        {
            throw new ArgumentException("保存文件夹不存在，请重新选择。");
        }

        if (!IsValidFileName(fileName))
        {
            throw new ArgumentException("文件名包含非法字符或为系统保留名。");
        }

        var target = Path.GetFullPath(Path.Combine(folder, fileName));
        var prefix = Path.TrimEndingDirectorySeparator(folder) + Path.DirectorySeparatorChar;
        if (!target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("文件名不能包含目录路径。");
        }

        var id = context.Tasks.EnqueueTracked(
            "下载 " + fileName,
            folder,
            target,
            new DownloadRequest
            {
                Kind = "file",
                Url = uri.AbsoluteUri
            });
        return new CustomDownloadInfo(id, target);
    }

    private static bool IsValidFileName(string name)
    {
        if (name.Length == 0 || name is "." or "..")
        {
            return false;
        }

        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return false;
        }

        if (name.EndsWith(' ') || name.EndsWith('.'))
        {
            return false;
        }

        var stem = Path.GetFileNameWithoutExtension(name).ToUpperInvariant();
        return Array.IndexOf(ReservedNames, stem) < 0;
    }
}
