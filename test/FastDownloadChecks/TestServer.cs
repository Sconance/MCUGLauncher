using System.Net;
using System.Net.Sockets;
using System.Text;

namespace FastDownloadChecks;

/// <summary>
/// 极简 TCP HTTP 服务器：不依赖 HttpListener 的 URLACL，可精确控制状态码、Range 支持与断连时机。
/// </summary>
internal sealed class TestServer : IAsyncDisposable
{
    readonly TcpListener listener;
    readonly CancellationTokenSource stop = new();
    readonly Task loop;
    int active;
    int peak;

    public TestServer()
    {
        listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        loop = Task.Run(AcceptAsync);
    }

    public int Port
    {
        get;
    }

    public int Peak => Volatile.Read(ref peak);

    public List<TestRequest> Requests
    {
        get;
    } = [];

    public Func<TestRequest, TestReply> Handler { get; set; } = _ => new TestReply([]);

    public Uri Url(string path) => new($"http://127.0.0.1:{Port}{path}");

    public int RequestCount
    {
        get
        {
            lock (Requests)
                return Requests.Count;
        }
    }

    public TestRequest[] Snapshot()
    {
        lock (Requests)
            return [.. Requests];
    }

    public async ValueTask DisposeAsync()
    {
        stop.Cancel();
        listener.Stop();
        try
        {
            await loop;
        }
        catch (Exception)
        {
        }

        stop.Dispose();
    }

    async Task AcceptAsync()
    {
        while (!stop.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(stop.Token);
            }
            catch (Exception)
            {
                return;
            }

            _ = Task.Run(() => HandleAsync(client));
        }
    }

    async Task HandleAsync(TcpClient client)
    {
        using (client)
        {
            var current = Interlocked.Increment(ref active);
            while (true)
            {
                var observed = Volatile.Read(ref peak);
                if (current <= observed || Interlocked.CompareExchange(ref peak, current, observed) == observed)
                    break;
            }

            try
            {
                var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, false, 4096, leaveOpen: true);
                var line = await reader.ReadLineAsync();
                if (line is null)
                    return;
                var parts = line.Split(' ');
                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                while (true)
                {
                    var header = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(header))
                        break;
                    var colon = header.IndexOf(':');
                    if (colon > 0)
                        headers[header[..colon].Trim()] = header[(colon + 1)..].Trim();
                }

                long? start = null;
                long? end = null;
                if (headers.TryGetValue("Range", out var range) && range.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
                {
                    var spec = range[6..].Split('-');
                    if (long.TryParse(spec[0], out var parsedStart))
                        start = parsedStart;
                    if (spec.Length > 1 && long.TryParse(spec[1], out var parsedEnd))
                        end = parsedEnd;
                }

                var request = new TestRequest(parts[0], parts[1], start, end);
                lock (Requests)
                    Requests.Add(request);
                var reply = Handler(request);
                if (reply.DelayMs > 0)
                    await Task.Delay(reply.DelayMs);
                var total = (long)reply.Body.Length;
                var status = reply.Status;
                var from = 0L;
                var to = total - 1;
                if (status == 206 && start.HasValue)
                {
                    from = start.Value;
                    to = end.HasValue ? Math.Min(end.Value, total - 1) : total - 1;
                }

                var builder = new StringBuilder();
                builder.Append("HTTP/1.1 ").Append(status).Append(' ').Append(Reason(status)).Append("\r\n");
                if (status == 416)
                {
                    builder.Append("Content-Range: bytes */").Append(total).Append("\r\n");
                    builder.Append("Content-Length: 0\r\n");
                }
                else
                {
                    if (!reply.OmitLength)
                        builder.Append("Content-Length: ").Append(to - from + 1).Append("\r\n");
                    if (status == 206)
                        builder.Append("Content-Range: bytes ").Append(from).Append('-').Append(to).Append('/').Append(total).Append("\r\n");
                    builder.Append("Accept-Ranges: bytes\r\n");
                }

                builder.Append("Connection: close\r\n\r\n");
                await stream.WriteAsync(Encoding.ASCII.GetBytes(builder.ToString()));
                if (status != 416 && status != 404 && status != 500)
                {
                    var count = (int)(to - from + 1);
                    if (reply.AbortAfter >= 0 && reply.AbortAfter < count)
                        count = reply.AbortAfter;
                    await stream.WriteAsync(reply.Body.AsMemory((int)from, count));
                    await stream.FlushAsync();
                }
            }
            catch (Exception)
            {
                // 客户端取消时读写会失败，属于预期情况。
            }
            finally
            {
                Interlocked.Decrement(ref active);
            }
        }
    }

    static string Reason(int status) => status switch
    {
        200 => "OK",
        206 => "Partial Content",
        404 => "Not Found",
        403 => "Forbidden",
        416 => "Range Not Satisfiable",
        429 => "Too Many Requests",
        500 => "Internal Server Error",
        503 => "Service Unavailable",
        _ => "Unknown"
    };
}

internal sealed record TestRequest(string Method, string Path, long? RangeStart, long? RangeEnd);

internal sealed record TestReply(byte[] Body, int Status = 200, int DelayMs = 0, int AbortAfter = -1, bool OmitLength = false);
