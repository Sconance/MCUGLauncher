using BlockLaunch;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Xml.Linq;

var root = Path.Combine(Path.GetTempPath(), "BlockLaunch-download-checks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var bytes = Enumerable.Range(0, 4096).Select(i => (byte)(i % 251)).ToArray();
var hash = Convert.ToHexString(SHA512.HashData(bytes));
var uri = new Uri("https://test.example/file");
foreach (var ignore in new[] {
    false,
    true
}

)
{
    var path = Path.Combine(root, "resume" + ignore);
    File.WriteAllBytes(ReliableDownloader.PartialPath(path, hash), bytes[..123]);
    using var http = new HttpClient(new Handler(request =>
    {
        if (request.Headers.Range?.Ranges.Single().From != 123)
            throw new Exception("Missing resume range");
        var response = new HttpResponseMessage(ignore ? HttpStatusCode.OK : HttpStatusCode.PartialContent)
        {
            Content = new ByteArrayContent(ignore ? bytes : bytes[123..])
        };
        if (!ignore)
            response.Content.Headers.ContentRange = new ContentRangeHeaderValue(123, bytes.Length - 1, bytes.Length);
        return response;
    }));
    await ReliableDownloader.Download(http, uri, path, hash, size: bytes.Length);
    if (!File.ReadAllBytes(path).SequenceEqual(bytes))
        throw new Exception("Resume corrupt");
    Console.WriteLine(ignore ? "RANGE_IGNORED_RESTART_OK" : "DURABLE_RANGE_RESUME_OK");
}

var attempts = 0;
var brokenPath = Path.Combine(root, "broken-stream");
var brokenRequests = 0;
using (var http = new HttpClient(new Handler(request =>
{
    brokenRequests++;
    if (brokenRequests == 1)
        return new(HttpStatusCode.OK)
        {
            Content = new StreamContent(new BrokenStream(bytes))
        };
    var offset = request.Headers.Range?.Ranges.Single().From ?? throw new Exception("Connection loss did not resume");
    if (offset != 128)
        throw new Exception("Wrong resumed offset");
    var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
    {
        Content = new ByteArrayContent(bytes[128..])
    };
    response.Content.Headers.ContentRange = new ContentRangeHeaderValue(128, bytes.Length - 1, bytes.Length);
    return response;
})))
{
    await ReliableDownloader.Download(http, uri, brokenPath, hash, size: bytes.Length);
    if (brokenRequests != 2 || !File.ReadAllBytes(brokenPath).SequenceEqual(bytes))
        throw new Exception("Broken stream retry failed");
    Console.WriteLine("CONNECTION_LOSS_RESUMES_WITH_CORRECT_BYTES_OK");
}

var cancelledPath = Path.Combine(root, "cancelled");
using (var cancellation = new CancellationTokenSource())
using (var http = new HttpClient(new Handler(_ => new(HttpStatusCode.OK) { Content = new StreamContent(new BrokenStream(bytes, cancellation)) })))
{
    try
    {
        await ReliableDownloader.Download(http, uri, cancelledPath, hash, size: bytes.Length, token: cancellation.Token);
        throw new Exception("Cancel ignored");
    }
    catch (OperationCanceledException)
    {
    }

    if (File.Exists(cancelledPath) || new FileInfo(ReliableDownloader.PartialPath(cancelledPath, hash)).Length != 128)
        throw new Exception("Cancellation lost partial");
    Console.WriteLine("CANCEL_RETAINS_PARTIAL_AND_DOES_NOT_COMMIT_OK");
}

using (var http = new HttpClient(new Handler(_ => ++attempts < 3 ? new(HttpStatusCode.ServiceUnavailable) : new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) })))
{
    await ReliableDownloader.Download(http, uri, Path.Combine(root, "retry"), hash);
    if (attempts != 3)
        throw new Exception("Retry count");
    Console.WriteLine("TRANSIENT_FAILURE_RETRY_OK");
}

var existing = Path.Combine(root, "protected");
File.WriteAllText(existing, "original");
using (var http = new HttpClient(new Handler(_ => new(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) })))
{
    try
    {
        await ReliableDownloader.Download(http, uri, existing, hash);
        throw new Exception("Bad hash accepted");
    }
    catch (IOException)
    {
    }

    if (File.ReadAllText(existing) != "original")
        throw new Exception("Original overwritten");
    Console.WriteLine("BAD_HASH_REJECTED_AND_TARGET_PRESERVED_OK");
}
// A locked destination must fail without downloading, and retain the original bytes.
var locked = Path.Combine(root, "locked");
File.WriteAllText(locked, "original");
var lockedRequests = 0;
using (var held = new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
using (var http = new HttpClient(new Handler(_ =>
{
    lockedRequests++;
    return new(HttpStatusCode.OK)
    {
        Content = new ByteArrayContent(bytes)
    };
})))
{
    try
    {
        await ReliableDownloader.Download(http, uri, locked, hash);
        throw new Exception("Locked file accepted");
    }
    catch (IOException ex)
    {
        if (!ex.Message.Contains("占用"))
            throw;
    }
}

if (lockedRequests != 0 || File.ReadAllText(locked) != "original")
    throw new Exception("Locked target was changed");
Console.WriteLine("LOCKED_TARGET_FAILS_CLEARLY_WITHOUT_NETWORK_AND_PRESERVES_FILE_OK");
var fullPath = Path.Combine(root, "diskfull-fixture");
File.WriteAllText(fullPath, "original");
var fullRequests = 0;
using (var http = new HttpClient(new Handler(_ =>
{
    fullRequests++;
    return new(HttpStatusCode.OK)
    {
        Content = new StreamContent(new DiskFullStream())
    };
})))
{
    try
    {
        await ReliableDownloader.Download(http, uri, fullPath, hash);
        throw new Exception("Disk full ignored");
    }
    catch (IOException ex)
    {
        if (!ex.Message.Contains("空间不足"))
            throw;
    }
}

if (fullRequests != 1 || File.ReadAllText(fullPath) != "original")
    throw new Exception("Disk-full retry or overwrite");
Console.WriteLine("SIMULATED_DISK_FULL_FAILS_ONCE_WITH_CLEAR_MESSAGE_AND_PRESERVES_FILE_OK");
var rangePath = Path.Combine(root, "range416");
File.WriteAllBytes(ReliableDownloader.PartialPath(rangePath, hash), bytes[..123]);
var rangeCalls = 0;
using (var http = new HttpClient(new Handler(r =>
{
    if (++rangeCalls == 1)
        return new(HttpStatusCode.RequestedRangeNotSatisfiable);
    if (r.Headers.Range != null)
        throw new Exception("Invalid partial not reset");
    return new(HttpStatusCode.OK)
    {
        Content = new ByteArrayContent(bytes)
    };
})))
{
    await ReliableDownloader.Download(http, uri, rangePath, hash);
}

if (rangeCalls != 2 || !File.ReadAllBytes(rangePath).SequenceEqual(bytes))
    throw new Exception("416 recovery failed");
Console.WriteLine("RANGE_416_RESTARTS_AND_VERIFIES_OK");
var invalidRange = Path.Combine(root, "bad-range");
File.WriteAllBytes(ReliableDownloader.PartialPath(invalidRange, hash), bytes[..123]);
var invalidCalls = 0;
using (var http = new HttpClient(new Handler(r =>
{
    invalidCalls++;
    var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
    {
        Content = new ByteArrayContent(bytes[123..])
    };
    response.Content.Headers.ContentRange = new ContentRangeHeaderValue(invalidCalls == 1 ? 124 : 123, bytes.Length - 1, bytes.Length);
    return response;
})))
{
    await ReliableDownloader.Download(http, uri, invalidRange, hash);
}

if (invalidCalls != 2 || !File.ReadAllBytes(invalidRange).SequenceEqual(bytes))
    throw new Exception("Invalid range accepted");
Console.WriteLine("INVALID_RANGE_NOT_APPENDED_AND_RETRY_VERIFIES_OK");
var readOnly = Path.Combine(root, "readonly-target");
File.WriteAllText(readOnly, "original");
var readOnlyCalls = 0;
try
{
    File.SetAttributes(readOnly, FileAttributes.ReadOnly);
    using var http = new HttpClient(new Handler(_ =>
    {
        readOnlyCalls++;
        return new(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(bytes)
        };
    }));
    try
    {
        await ReliableDownloader.Download(http, uri, readOnly, hash);
        throw new Exception("Read-only destination overwritten");
    }
    catch (IOException ex)
    {
        if (!ex.Message.Contains("权限") && !ex.Message.Contains("只读"))
            throw;
    }

    if (readOnlyCalls != 1 || File.ReadAllText(readOnly) != "original" || !File.ReadAllBytes(ReliableDownloader.PartialPath(readOnly, hash)).SequenceEqual(bytes))
        throw new Exception("Read-only failure lost original or verified cache");
    File.SetAttributes(readOnly, FileAttributes.Normal);
    await ReliableDownloader.Download(http, uri, readOnly, hash);
    if (readOnlyCalls != 1 || !File.ReadAllBytes(readOnly).SequenceEqual(bytes))
        throw new Exception("Verified download was not reused after permissions restored");
    Console.WriteLine("READONLY_TARGET_PRESERVED_AND_RETRY_REUSES_VERIFIED_FILE_WITHOUT_NETWORK_OK");
}
finally
{
    File.SetAttributes(readOnly, FileAttributes.Normal);
}

var versions = XDocument.Parse("<metadata><versioning><versions><version>21.1.7</version><version>21.10.9</version><version>21.1.8-beta</version><version>1.21.1-52.1.0</version></versions></versioning></metadata>");
if (LoaderCatalog.Parse(versions, "1.21.1", "neoforge").Count != 2 || LoaderCatalog.Parse(versions, "1.20.1", "neoforge").Count != 0 || LoaderCatalog.Parse(versions, "1.21.1", "forge").Single().Version != "52.1.0")
    throw new Exception("Catalog mapping");
Console.WriteLine("LOADER_VERSION_MAPPING_OK");
Console.WriteLine("ALL_DOWNLOAD_CHECKS_PASSED");
sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> handle) : HttpMessageHandler
{
    /// <summary>
    /// 发送。
    /// </summary>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(handle(request));
}

sealed class BrokenStream(byte[] bytes, CancellationTokenSource? cancellation = null) : MemoryStream(new byte[bytes.Length])
{
    int reads;

    /// <summary>
    /// 读取。
    /// </summary>
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
    {
        if (++reads == 1)
        {
            bytes.AsMemory(0, 128).CopyTo(buffer);
            return ValueTask.FromResult(128);
        }

        if (cancellation != null)
        {
            cancellation.Cancel();
            return ValueTask.FromCanceled<int>(token);
        }

        throw new IOException("simulated dropped connection");
    }
}

sealed class DiskFullStream : MemoryStream
{
    /// <summary>
    /// 读取。
    /// </summary>
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default) => ValueTask.FromException<int>(new IOException("simulated disk full", unchecked((int)0x80070070)));
}
