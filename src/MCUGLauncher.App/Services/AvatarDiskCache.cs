using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MCUGLauncher;

namespace MCUGLauncher.Services;

/// <summary>A 类：持久化头像图片、账号索引与来源索引；只依赖 Core/BCL，不读取账号或访问网络。</summary>
internal sealed class AvatarDiskCache(string directory)
{
    const int Format = 1;
    const int MaximumBytes = 2 * 1024 * 1024;
    readonly object gate = new();
    readonly Dictionary<string, long> committed = [];
    sealed record Index(int Format, string Image, string Source, DateTimeOffset Checked);
    internal sealed record Entry(string Image, DateTimeOffset Checked);
    static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    string IndexPath(string group, string value) => Path.Combine(directory, group, Hash(value) + ".json");
    string ImagePath(string hash) => Path.Combine(directory, "images", hash + ".png");
    public Task<Entry?> ReadAsync(string identity, CancellationToken token) => Read(IndexPath("accounts", identity), token);
    public Task<Entry?> ReadSourceAsync(string source, CancellationToken token) => Read(IndexPath("sources", source), token);
    Task<Entry?> Read(string path, CancellationToken token) => Task.Run<Entry?>(() =>
    {
        token.ThrowIfCancellationRequested();
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length > 4096)
                return null;
            var index = AtomicFile.ReadJson<Index>(path);
            if (index.Format != Format || index.Image is not { Length: 64 } || !index.Image.All(Uri.IsHexDigit))
                return null;
            var imagePath = ImagePath(index.Image);
            if (!File.Exists(imagePath) || new FileInfo(imagePath).Length > MaximumBytes)
                return null;
            using var input = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            if (input.Length > MaximumBytes)
                return null;
            var bytes = new byte[(int)input.Length];
            input.ReadExactly(bytes);
            if (Convert.ToHexString(SHA256.HashData(bytes)) != index.Image || !IsPng(bytes))
                return null;
            return new("data:image/png;base64," + Convert.ToBase64String(bytes), index.Checked);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ArgumentException) { return null; }
    }, token);

    public Task SaveAsync(string identity, string source, string image, long revision, CancellationToken token) => Task.Run(() =>
    {
        if (!image.StartsWith("data:image/png;base64,"))
            return;
        var bytes = Convert.FromBase64String(image[22..]);
        if (!IsPng(bytes))
            return;
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        var index = new Index(Format, hash, Hash(source), DateTimeOffset.UtcNow);
        lock (gate)
        {
            token.ThrowIfCancellationRequested();
            if (committed.GetValueOrDefault(identity) > revision)
                return;
            var file = ImagePath(hash);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            var temp = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    output.Write(bytes);
                    output.Flush(true);
                }
                token.ThrowIfCancellationRequested();
                File.Move(temp, file, true);
                AtomicFile.WriteJson(IndexPath("sources", source), index);
                AtomicFile.WriteJson(IndexPath("accounts", identity), index);
                committed[identity] = revision;
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }, token);

    // Cached previews are created by the injected renderer; bound size and content digest contain corruption.
    static bool IsPng(byte[] bytes)
    {
        if (bytes.Length < 33 || bytes.Length > MaximumBytes || !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ||
            !bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8))
            return false;
        var width = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16, 4));
        var height = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20, 4));
        return width > 0 && width <= 1152 && width == height;
    }
}
