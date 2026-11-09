using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MCUGLauncher.Services;

internal enum AnnouncementSyncOutcome
{
    Updated, Unchanged, Cached, Failed
}

internal sealed record AnnouncementSyncStatus(
    AnnouncementSyncOutcome Outcome, DateTimeOffset? LastSuccess, string Error);

/// <summary>从 HTTPS 清单同步公告，校验内容后一次性提交本地快照。</summary>
internal sealed class AnnouncementSyncService(HttpClient http, string? cacheDirectory)
{
    public const string Primary = "https://raw.githubusercontent.com/Sconance/MCUGL-Config/main/sources.json";
    const string Fallback = "https://gitee.com/Sconance/MCUGL-Config/raw/main/sources.json";
    const int FileLimit = 1024 * 1024;
    const int SnapshotLimit = 16 * FileLimit;
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    static readonly UTF8Encoding Utf8 = new(false, true);
    readonly SemaphoreSlim gate = new(1);
    volatile AnnouncementSyncStatus? lastStatus;

    public bool IsSyncing => gate.CurrentCount == 0;
    public AnnouncementSyncStatus? LastStatus => lastStatus;

    public static bool IsSyncSource(Uri uri) =>
        uri.AbsolutePath.EndsWith("/sources.json", StringComparison.OrdinalIgnoreCase)
        || uri.AbsolutePath.EndsWith("/manifest.json", StringComparison.OrdinalIgnoreCase);

    public async Task<(T[] Items, AnnouncementSyncStatus Status)> SyncAsync<T>(
        Uri source, Func<string, string, T> parse, CancellationToken token)
    {
        await gate.WaitAsync(token);
        try
        {
            var cacheFile = cacheDirectory is null ? null : Path.Combine(cacheDirectory,
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source.AbsoluteUri))) + ".json");
            var old = await LoadAsync(cacheFile, parse, token);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(TimeSpan.FromSeconds(40));
            try
            {
                var (snapshot, items) = await FetchAsync(source, old, parse, deadline.Token);
                if (cacheFile is not null)
                    await SaveAsync(cacheFile, snapshot, deadline.Token);
                var outcome = old is not null && old.Files.Select(file => file.Entry)
                    .SequenceEqual(snapshot.Files.Select(file => file.Entry))
                    ? AnnouncementSyncOutcome.Unchanged : AnnouncementSyncOutcome.Updated;
                return (items, lastStatus = new(outcome, snapshot.SyncedAt, ""));
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                return UseCache(old, parse, "公告同步超时。");
            }
            catch (Exception error) when (IsSyncError(error))
            {
                return UseCache(old, parse, error.Message);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    async Task<(Snapshot Snapshot, T[] Items)> FetchAsync<T>(Uri source, Snapshot? old, Func<string, string, T> parse,
        CancellationToken token)
    {
        string error = "";
        Uri[] sources = source.AbsoluteUri == Primary ? [source, new Uri(Fallback)] : [source];
        foreach (var address in sources)
        {
            try
            {
                if (address.AbsolutePath.EndsWith("/manifest.json", StringComparison.OrdinalIgnoreCase))
                    return await FetchManifestAsync(address, old, parse, token);

                var index = await ReadJsonAsync<SourceIndex>(address, token);
                if (index.SchemaVersion != 1 || index.Resources?.Announcements is not { } resource)
                    throw new InvalidDataException("公告源索引格式无效。");
                foreach (var url in new[] { resource.Primary }.Concat(resource.Mirrors ?? []))
                {
                    try
                    {
                        var root = HttpsUri(url);
                        if (!root.AbsolutePath.EndsWith('/') || root.Query.Length > 0)
                            throw new InvalidDataException("公告资源地址必须指向 HTTPS 目录。");
                        return await FetchManifestAsync(new Uri(root, "manifest.json"), old, parse, token);
                    }
                    catch (Exception failure) when (IsSyncError(failure))
                    {
                        error = failure.Message;
                    }
                }
            }
            catch (Exception failure) when (IsSyncError(failure))
            {
                error = failure.Message;
            }
        }
        throw new IOException("公告同步失败：" + error);
    }

    async Task<(Snapshot Snapshot, T[] Items)> FetchManifestAsync<T>(Uri manifestUrl, Snapshot? old, Func<string, string, T> parse,
        CancellationToken token)
    {
        var manifest = await ReadJsonAsync<Manifest>(manifestUrl, token);
        if (manifest.SchemaVersion != 1 || manifest.Files is null
            || manifest.Files.Any(entry => string.IsNullOrWhiteSpace(entry?.Path)))
            throw new InvalidDataException("公告清单格式无效。");

        var entries = manifest.Files.Where(entry => entry.Path.EndsWith(".md",
            StringComparison.OrdinalIgnoreCase)).ToArray();
        if (entries.Length > 64 || entries.Any(entry => !ValidEntry(entry))
            || entries.Sum(entry => (long)entry.Size) > SnapshotLimit
            || entries.Select(entry => entry.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != entries.Length)
            throw new InvalidDataException("公告清单的路径、大小或 SHA-256 无效。");

        var previous = old?.Files.ToDictionary(file => file.Entry.Path, StringComparer.Ordinal);
        var files = new List<CachedFile>();
        foreach (var original in entries.OrderByDescending(entry => FileDate(entry.Path))
                     .ThenByDescending(entry => entry.Path, StringComparer.Ordinal))
        {
            var entry = original with
            {
                Sha256 = original.Sha256.ToUpperInvariant()
            };
            var url = new Uri(manifestUrl, string.Join('/', entry.Path.Split('/').Select(Uri.EscapeDataString)));
            var bytes = previous is not null && previous.TryGetValue(entry.Path, out var file) && file.Entry == entry
                ? file.Bytes : await DownloadAsync(url, entry.Size, token);
            if (!Matches(entry, bytes))
                throw new InvalidDataException($"公告文件校验失败：{entry.Path}");
            files.Add(new(entry, url.AbsoluteUri, bytes));
        }
        var snapshot = new Snapshot(DateTimeOffset.UtcNow, [.. files]);
        // 清单中每篇公告都必须可解析，才允许替换上次成功的快照。
        return (snapshot, Parse(snapshot, parse));
    }

    static bool ValidEntry(Entry entry) => entry.Size is > 0 and <= FileLimit
        && entry.Sha256 is { Length: 64 } && entry.Sha256.All(Uri.IsHexDigit)
        && entry.Path.Split('/').All(part => part.Length > 0 && part is not ("." or "..")
            && !part.Any(ch => char.IsControl(ch) || ch is '\\' or ':' or '%' or '?' or '#'));

    static bool Matches(Entry entry, byte[] bytes) => bytes.Length == entry.Size
        && Convert.ToHexString(SHA256.HashData(bytes)).Equals(entry.Sha256, StringComparison.OrdinalIgnoreCase);

    public static DateTimeOffset? FileDate(string path)
    {
        var name = Path.GetFileName(path);
        return name.Length >= 10 && DateOnly.TryParseExact(name.AsSpan(0, 10), "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) : null;
    }

    static Uri HttpsUri(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
        && uri.UserInfo.Length == 0 && uri.Fragment.Length == 0
            ? uri : throw new InvalidDataException("公告同步地址必须使用 HTTPS。");

    async Task<T> ReadJsonAsync<T>(Uri url, CancellationToken token) =>
        JsonSerializer.Deserialize<T>(await DownloadAsync(url, FileLimit, token), JsonOptions)
        ?? throw new InvalidDataException("公告清单为空。");

    async Task<byte[]> DownloadAsync(Uri url, int limit, CancellationToken token)
    {
        HttpsUri(url.AbsoluteUri);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(8));
        try
        {
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            response.EnsureSuccessStatusCode();
            HttpsUri((response.RequestMessage?.RequestUri ?? url).AbsoluteUri);
            if (response.Content.Headers.ContentLength > limit)
                throw new InvalidDataException("公告响应超过大小限制。");
            await using var input = await response.Content.ReadAsStreamAsync(deadline.Token);
            using var output = new MemoryStream();
            var buffer = new byte[8192];
            int count;
            while ((count = await input.ReadAsync(buffer, deadline.Token)) > 0)
            {
                if (output.Length + count > limit)
                    throw new InvalidDataException("公告响应超过大小限制。");
                output.Write(buffer, 0, count);
            }
            return output.ToArray();
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            throw new IOException("公告请求超时。");
        }
    }

    static bool IsSyncError(Exception error) => error is HttpRequestException or IOException
        or InvalidDataException or UnauthorizedAccessException or JsonException or DecoderFallbackException;

    static T[] Parse<T>(Snapshot snapshot, Func<string, string, T> parse) =>
        snapshot.Files.Select(file => parse(Utf8.GetString(file.Bytes), file.Url)).ToArray();

    (T[], AnnouncementSyncStatus) UseCache<T>(Snapshot? old, Func<string, string, T> parse, string error)
    {
        lastStatus = old is null ? new(AnnouncementSyncOutcome.Failed, null, error)
            : new(AnnouncementSyncOutcome.Cached, old.SyncedAt, error);
        return (old is null ? [] : Parse(old, parse), lastStatus);
    }

    static async Task<Snapshot?> LoadAsync<T>(string? path, Func<string, string, T> parse, CancellationToken token)
    {
        if (path is null || !File.Exists(path))
            return null;
        try
        {
            await using var input = File.OpenRead(path);
            if (input.Length > 2 * SnapshotLimit)
                return null;
            var snapshot = await JsonSerializer.DeserializeAsync<Snapshot>(input, JsonOptions, token);
            if (snapshot?.Files is not { Length: <= 64 } || snapshot.Files.Any(file =>
                file?.Entry?.Path is null || !ValidEntry(file.Entry) || file.Bytes is null
                || !Matches(file.Entry, file.Bytes) || HttpsUri(file.Url) is null)
                || snapshot.Files.Select(file => file.Entry.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count()
                    != snapshot.Files.Length)
                return null;
            _ = Parse(snapshot, parse);
            return snapshot;
        }
        catch (Exception error) when (IsSyncError(error))
        {
            return null;
        }
    }

    static async Task SaveAsync(string path, Snapshot snapshot, CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var output = File.Create(temporary))
                await JsonSerializer.SerializeAsync(output, snapshot, JsonOptions, token);
            token.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    sealed record SourceIndex(int SchemaVersion, ResourceIndex? Resources);
    sealed record ResourceIndex(Resource? Announcements);
    sealed record Resource(string? Primary, string[]? Mirrors);
    sealed record Manifest(int SchemaVersion, Entry[]? Files);
    sealed record Entry(string Path, int Size, string Sha256);
    sealed record CachedFile(Entry Entry, string Url, byte[] Bytes);
    sealed record Snapshot(DateTimeOffset SyncedAt, CachedFile[] Files);
}
