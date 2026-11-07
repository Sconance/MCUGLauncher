using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MCUGLauncher.Contracts;

namespace MCUGLauncher.Services;

/// <summary>
/// 社区数据源访问。
/// </summary>
/// <remarks>
/// 组件级别：B。通过 HttpClient 读取社区数据，通过 AnnouncementInfo 的构造函数和
/// Markdown、PublishedAt 属性返回公告。与界面无直接依赖。
/// </remarks>
public sealed class CommunityService
{
    const int MaxAnnouncementBytes = 1024 * 1024;
    const int MaxDirectoryAnnouncements = 10;
    readonly HttpClient _http;
    public CommunityService(HttpClient http) => _http = http;

    public async Task<(bool Ok, string Markdown, string Error, int Ms)> ReadAnnouncementAsync(
        string url,
        CancellationToken token)
    {
        if (!TryHttpUri(url, out var uri))
            return (false, "", "公告数据源必须是有效的 HTTP(S) 地址。", 0);

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(8));
        var watch = Stopwatch.StartNew();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            if (IsGitHubDirectory(uri))
            {
                request.Headers.UserAgent.ParseAdd("MCUGLauncher");
                request.Headers.Accept.ParseAdd("application/vnd.github+json");
            }
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (!response.IsSuccessStatusCode)
                return (false, "", $"公告读取失败：HTTP {(int)response.StatusCode} · {response.ReasonPhrase}", (int)watch.ElapsedMilliseconds);
            if (response.Content.Headers.ContentLength > MaxAnnouncementBytes)
                return (false, "", "公告文件或目录清单不能超过 1 MB。", (int)watch.ElapsedMilliseconds);

            await using var input = await response.Content.ReadAsStreamAsync(deadline.Token);
            using var output = new MemoryStream();
            var buffer = new byte[8192];
            int read;
            while ((read = await input.ReadAsync(buffer, deadline.Token)) > 0)
            {
                if (output.Length + read > MaxAnnouncementBytes)
                    return (false, "", "公告文件或目录清单不能超过 1 MB。", (int)watch.ElapsedMilliseconds);
                await output.WriteAsync(buffer.AsMemory(0, read), deadline.Token);
            }

            watch.Stop();
            return (true, Encoding.UTF8.GetString(output.ToArray()).TrimStart('\uFEFF'), "", (int)watch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            return (false, "", "公告读取超时（8 秒）。", 8000);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            return (false, "", "公告读取失败：" + ex.Message, (int)watch.ElapsedMilliseconds);
        }
    }

    public async Task<(bool Ok, string Text, int Ms)> TestAnnouncementAsync(string url, CancellationToken token)
    {
        var result = await ReadAnnouncementsAsync(url, token);
        if (result.Items.Length == 0)
            return (false, result.Error, result.Ms);
        var text = result.IsDirectory
            ? $"GitHub 公告目录有效 · 成功读取 {result.Items.Length} 篇 · {result.Ms} ms"
            : $"Markdown 公告有效 · {result.Ms} ms";
        return (true, text, result.Ms);
    }

    /// <summary>读取单篇公告，或读取 GitHub 目录中最近的公告。</summary>
    public async Task<(AnnouncementInfo[] Items, string Error, int Ms, bool IsDirectory)> ReadAnnouncementsAsync(
        string url,
        CancellationToken token)
    {
        var isDirectory = TryHttpUri(url, out var uri) && IsGitHubDirectory(uri);
        var read = await ReadAnnouncementAsync(url, token);
        if (!read.Ok)
            return ([], read.Error, read.Ms, isDirectory);
        if (isDirectory)
            return await ReadDirectoryAnnouncementsAsync(read.Markdown, read.Ms, token);

        try
        {
            return ([ParseMarkdownAnnouncement(read.Markdown, url)], "", read.Ms, false);
        }
        catch (InvalidDataException ex)
        {
            return ([], "公告 Markdown 无效：" + ex.Message, read.Ms, false);
        }
    }

    async Task<(AnnouncementInfo[] Items, string Error, int Ms, bool IsDirectory)> ReadDirectoryAnnouncementsAsync(
        string json,
        int listingMs,
        CancellationToken token)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            using var listing = JsonDocument.Parse(json);
            if (listing.RootElement.ValueKind != JsonValueKind.Array)
                return ([], "公告源须指向 GitHub Contents API 的目录。", listingMs, true);

            var files = listing.RootElement.EnumerateArray()
                .Select(ParseDirectoryFile)
                .OfType<AnnouncementFile>()
                .OrderByDescending(file => file.Date)
                .ThenByDescending(file => file.Name, StringComparer.Ordinal)
                .Take(MaxDirectoryAnnouncements);
            var results = await Task.WhenAll(files.Select(file => ReadDirectoryFileAsync(file, token)));
            var announcements = results.OfType<AnnouncementInfo>().ToArray();
            var error = results.Length == 0
                ? "公告目录中没有可下载的 Markdown 文件。"
                : announcements.Length == 0 ? "公告目录中的 Markdown 文件均未能读取或解析。" : "";
            return (announcements, error, listingMs + (int)watch.ElapsedMilliseconds, true);
        }
        catch (JsonException)
        {
            return ([], "GitHub 公告目录返回了无效的文件清单。", listingMs, true);
        }
    }

    async Task<AnnouncementInfo?> ReadDirectoryFileAsync(AnnouncementFile file, CancellationToken token)
    {
        var read = await ReadAnnouncementAsync(file.Url, token);
        if (!read.Ok)
            return null;
        try
        {
            return ParseMarkdownAnnouncement(read.Markdown, file.Url) with
            {
                PublishedAt = file.Date
            };
        }
        catch (InvalidDataException)
        {
            // 单篇损坏时仍展示同批次的其他公告。
            return null;
        }
    }

    static AnnouncementFile? ParseDirectoryFile(JsonElement item)
    {
        if (ReadString(item, "type") != "file"
            || ReadString(item, "name") is not { } name
            || !name.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            || ReadString(item, "download_url") is not { } url
            || !TryHttpUri(url, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !uri.IsDefaultPort
            || uri.Host != "raw.githubusercontent.com")
            return null;

        DateTimeOffset? published = null;
        if (name.Length >= 10
            && DateOnly.TryParseExact(name.AsSpan(0, 10), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
            published = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        return new AnnouncementFile(name, url, published);
    }

    static string? ReadString(JsonElement item, string key) =>
        item.ValueKind == JsonValueKind.Object
        && item.TryGetProperty(key, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    static bool IsGitHubDirectory(Uri uri)
    {
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort && uri.Host == "api.github.com"
            && segments.Length >= 4 && segments[0] == "repos" && segments[3] == "contents";
    }

    sealed record AnnouncementFile(string Name, string Url, DateTimeOffset? Date);

    public static AnnouncementInfo ParseMarkdownAnnouncement(string markdown, string source)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            throw new InvalidDataException("公告 Markdown 不能为空。");

        markdown = markdown.TrimStart('\uFEFF').Replace("\r\n", "\n").Replace('\r', '\n');
        try
        {
            using var json = JsonDocument.Parse(markdown);
            if (json.RootElement.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
                throw new InvalidDataException("公告源须使用 Markdown 文件地址或 GitHub Contents API 目录地址。");
        }
        catch (JsonException)
        {
        }

        var lines = markdown.Split('\n');
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        var fullMarkdown = new StringBuilder();
        var fence = '\0';
        var fenceLength = 0;
        for (var index = 0; index < lines.Length;)
        {
            var line = lines[index];
            if (TryFence(line, ref fence, ref fenceLength))
            {
                fullMarkdown.AppendLine(line);
                index++;
                continue;
            }

            var field = fence == '\0' ? MetadataField(line) : null;
            if (field is null)
            {
                fullMarkdown.AppendLine(line);
                index++;
                continue;
            }
            if (!fields.TryAdd(field, ""))
                throw new InvalidDataException($"公告 Markdown 的 #{field} 字段重复。");

            var valueLines = new List<string>();
            index++;
            var valueFence = '\0';
            var valueFenceLength = 0;
            while (index < lines.Length)
            {
                var valueLine = lines[index];
                if (valueFence == '\0' && IsLevelOneHeading(valueLine))
                    break;
                TryFence(valueLine, ref valueFence, ref valueFenceLength);
                valueLines.Add(valueLine);
                index++;
            }

            fields[field] = TrimBlankLines(valueLines);
        }

        var title = GetField(fields, "preview_title");
        var body = GetField(fields, "preview_body");
        var imageUrl = GetField(fields, "preview_pic_url");
        var tags = GetField(fields, "tags")
            .Split([',', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var author = GetField(fields, "author");
        if (new[] { title, body, imageUrl, string.Join("", tags), author }.All(string.IsNullOrWhiteSpace))
            body = "一份很懒的公告";

        if (imageUrl.Length > 0 && !TryHttpUri(imageUrl, out _))
            throw new InvalidDataException("preview_pic_url 必须是有效的 HTTP(S) 图片地址。");

        var rawMarkdown = fullMarkdown.ToString().TrimEnd('\n');
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source + "\n" + markdown)))[..16];
        return new AnnouncementInfo(id, title, body, string.Join(" · ", tags), author, null, imageUrl)
        {
            Markdown = rawMarkdown
        };
    }

    static string? MetadataField(string line)
    {
        var heading = line.TrimEnd();
        return heading is "# preview_title" or "# preview_body" or "# preview_pic_url" or "# tags" or "# author"
            ? heading[2..]
            : null;
    }

    static bool IsLevelOneHeading(string line) =>
        line == "#" || line.StartsWith("# ", StringComparison.Ordinal);

    static bool TryFence(string line, ref char fence, ref int fenceLength)
    {
        var trimmed = line.TrimStart();
        if (trimmed.Length < 3 || trimmed[0] is not ('`' or '~'))
            return false;
        var marker = trimmed[0];
        var length = 0;
        while (length < trimmed.Length && trimmed[length] == marker)
            length++;
        if (length < 3)
            return false;

        if (fence == '\0')
        {
            fence = marker;
            fenceLength = length;
        }
        else if (marker == fence && length >= fenceLength)
        {
            fence = '\0';
            fenceLength = 0;
        }
        return true;
    }

    static string TrimBlankLines(IReadOnlyList<string> lines)
    {
        var first = 0;
        var last = lines.Count;
        while (first < last && string.IsNullOrWhiteSpace(lines[first]))
            first++;
        while (last > first && string.IsNullOrWhiteSpace(lines[last - 1]))
            last--;
        return string.Join("\n", lines.Skip(first).Take(last - first)).Trim();
    }

    static string GetField(IReadOnlyDictionary<string, string> fields, string name) =>
        fields.TryGetValue(name, out var value) ? value : "";

    static bool TryHttpUri(string value, out Uri uri) =>
        Uri.TryCreate(value, UriKind.Absolute, out uri!)
        && uri.UserInfo.Length == 0
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    public async Task<(bool Ok, string Text, int Ms)> TestAsync(string url, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(8));
        token = deadline.Token;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            return (false, "地址需以 https:// 或 http:// 开头", 0);

        var watch = Stopwatch.StartNew();
        try
        {
            using var response = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
            watch.Stop();
            return response.IsSuccessStatusCode
                ? (true, $"连接正常 · {watch.ElapsedMilliseconds} ms", (int)watch.ElapsedMilliseconds)
                : (false, $"HTTP {(int)response.StatusCode} · {response.ReasonPhrase}", (int)watch.ElapsedMilliseconds);
        }
        catch (TaskCanceledException)
        {
            return (false, "连接超时（8s）", 8000);
        }
        catch (HttpRequestException ex)
        {
            return (false, ex.Message, (int)watch.ElapsedMilliseconds);
        }
    }

    /// <summary>
    /// 读取数据源文本。
    /// </summary>
    /// <param name="url">数据源地址</param>
    /// <param name="token">取消令牌</param>
    /// <returns>响应文本；失败时为 null</returns>
    /// <remarks>
    /// 成功返回响应体，失败返回 null，不抛出网络异常。
    /// </remarks>
    public async Task<string?> ReadAsync(string url, CancellationToken token)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
            return null;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(8));
        token = deadline.Token;
        try
        {
            using var response = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
            if (!response.IsSuccessStatusCode)
                return null;
            return await response.Content.ReadAsStringAsync(token);
        }
        catch (Exception ex) when (ex is TaskCanceledException or HttpRequestException)
        {
            return null;
        }
    }
}
