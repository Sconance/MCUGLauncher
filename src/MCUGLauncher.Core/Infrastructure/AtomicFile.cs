using System.Text.Json;

namespace BlockLaunch;

/// <summary>
/// 原子文件写入。
/// </summary>
/// <remarks>
/// 通过同目录临时文件 + 替换避免写入中断留下半个文件；不负责跨卷移动。
/// </remarks>
public static class AtomicFile
{
    public static void WriteJson<T>(string file, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
        var temp = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, value, new JsonSerializerOptions { WriteIndented = true });
                stream.Flush(true);
            }

            File.Move(temp, file, true);
        }
        finally
        {
            if (File.Exists(temp))
                File.Delete(temp);
        }
    }

    public static T ReadJson<T>(string file)
    {
        // Readers hold an immutable snapshot while atomic writers may replace the name.
        using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        return JsonSerializer.Deserialize<T>(input) ?? throw new InvalidDataException("配置内容为空：" + file);
    }
}
