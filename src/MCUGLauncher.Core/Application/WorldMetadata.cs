using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace BlockLaunch;

/// <summary>
/// 表示 WorldMetadata 数据。
/// </summary>
public record WorldMetadata(string Name, string Version, int DataVersion, bool Snapshot)
{
    /// <summary>
    /// 读取。
    /// </summary>
    public static WorldMetadata Read(string file)
    {
        using var source = File.OpenRead(file);
        using var gzip = new GZipStream(source, CompressionMode.Decompress);
        using var bytes = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = gzip.Read(buffer)) > 0)
        {
            if (bytes.Length + count > 16 * 1024 * 1024)
                throw new IOException("level.dat 解压后超过 16 MiB。");
            bytes.Write(buffer, 0, count);
        }

        bytes.Position = 0;
        using var reader = new BinaryReader(bytes, Encoding.UTF8);
        var values = new Dictionary<string, object>();
        var budget = 200000;
        byte Byte() => reader.ReadByte();
        int Int() => BinaryPrimitives.ReadInt32BigEndian(reader.ReadBytes(4).AsSpan());
        string Text()
        {
            var length = BinaryPrimitives.ReadUInt16BigEndian(reader.ReadBytes(2).AsSpan());
            var data = reader.ReadBytes(length);
            if (data.Length != length)
                throw new EndOfStreamException();
            return Encoding.UTF8.GetString(data);
        }

        void Skip(long length)
        {
            if (length < 0 || length > bytes.Length - bytes.Position)
                throw new IOException("NBT 长度无效。");
            bytes.Position += length;
        }

        void Tag(byte type, string path, int depth)
        {
            if (depth > 64 || --budget < 0)
                throw new IOException("NBT 结构过深或过大。");
            switch (type)
            {
                case 1:
                    var b = Byte();
                    if (path == "Data/Version/Snapshot")
                        values[path] = b != 0;
                    break;
                case 2:
                    Skip(2);
                    break;
                case 3:
                    var n = Int();
                    if (path == "Data/DataVersion")
                        values[path] = n;
                    break;
                case 4:
                case 6:
                    Skip(8);
                    break;
                case 5:
                    Skip(4);
                    break;
                case 7:
                    Skip(Int());
                    break;
                case 8:
                    var text = Text();
                    if (path is "Data/LevelName" or "Data/Version/Name")
                        values[path] = text;
                    break;
                case 9:
                    var child = Byte();
                    var size = Int();
                    if (size < 0 || size > 200000 || child == 0 && size > 0)
                        throw new IOException("NBT 列表无效。");
                    for (var i = 0; i < size; i++)
                        Tag(child, "", depth + 1);
                    break;
                case 10:
                    while (true)
                    {
                        var next = Byte();
                        if (next == 0)
                            break;
                        var name = Text();
                        Tag(next, path.Length == 0 ? name : path + "/" + name, depth + 1);
                    }

                    break;
                case 11:
                    Skip(checked((long)Int() * 4));
                    break;
                case 12:
                    Skip(checked((long)Int() * 8));
                    break;
                default:
                    throw new IOException("未知 NBT 类型。");
            }
        }

        if (Byte() != 10)
            throw new IOException("level.dat 根节点无效。");
        Text();
        Tag(10, "", 0);
        return new(values.GetValueOrDefault("Data/LevelName") as string ?? Path.GetFileName(Path.GetDirectoryName(file))!, values.GetValueOrDefault("Data/Version/Name") as string ?? "未知", values.GetValueOrDefault("Data/DataVersion") is int version ? version : 0, values.GetValueOrDefault("Data/Version/Snapshot") is true);
    }

    /// <summary>
    /// 执行 Warning。
    /// </summary>
    public string Warning(string target) => Version == "未知" ? "未取得存档版本，请先备份。" : ModPreflight.Matches(target, ">=" + Version) == false ? $"存档来自 {Version}，当前目标 {target} 可能降级并损坏世界；建议使用备份副本。" : Snapshot ? "该世界曾由快照版本保存，切换游戏版本前请备份。" : "跨版本首次打开前建议备份。";
}
