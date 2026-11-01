using System.Net;

namespace BlockLaunch;

// Counts bytes actually consumed from HTTP responses for the current download operation.
/// <summary>
/// 提供 下载Transfer处理器 相关功能。
/// </summary>
public sealed class DownloadTransferHandler(HttpMessageHandler inner, Func<int>? limitKib = null) : DelegatingHandler(inner)
{
    readonly TransferLimiter limiter = new(limitKib);

    /// <summary>
    /// 发送。
    /// </summary>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        var operation = OperationExecution.Ambient.Value;
        var response = await base.SendAsync(request, token);
        if (operation?.Record.Download != null && response.Content != null)
            response.Content = new MeteredContent(response.Content, operation, limiter);
        return response;
    }

    sealed class MeteredContent : HttpContent
    {
        readonly HttpContent inner;
        readonly OperationContext operation;
        readonly TransferLimiter limiter;

        public MeteredContent(HttpContent inner, OperationContext operation, TransferLimiter limiter)
        {
            this.inner = inner;
            this.operation = operation;
            this.limiter = limiter;
            foreach (var header in inner.Headers)
                Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        /// <summary>
        /// 尝试ComputeLength。
        /// </summary>
        protected override bool TryComputeLength(out long length)
        {
            length = inner.Headers.ContentLength ?? 0;
            return inner.Headers.ContentLength.HasValue;
        }

        /// <summary>
        /// 执行 SerializeToStream。
        /// </summary>
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            using var input = new MeteredStream(await inner.ReadAsStreamAsync(), operation, limiter);
            await input.CopyToAsync(stream);
        }

        /// <summary>
        /// 执行 SerializeToStream。
        /// </summary>
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken token)
        {
            using var input = new MeteredStream(await inner.ReadAsStreamAsync(token), operation, limiter);
            await input.CopyToAsync(stream, token);
        }

        /// <summary>
        /// 创建内容ReadStream。
        /// </summary>
        protected override async Task<Stream> CreateContentReadStreamAsync() => new MeteredStream(await inner.ReadAsStreamAsync(), operation, limiter);
        /// <summary>
        /// 创建内容ReadStream。
        /// </summary>
        protected override async Task<Stream> CreateContentReadStreamAsync(CancellationToken token) => new MeteredStream(await inner.ReadAsStreamAsync(token), operation, limiter);

        /// <summary>
        /// 执行 Dispose。
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                inner.Dispose();
            base.Dispose(disposing);
        }
    }

    sealed class MeteredStream(Stream inner, OperationContext operation, TransferLimiter limiter) : Stream
    {
        public override bool CanRead => inner.CanRead;

        public override bool CanSeek => inner.CanSeek;

        public override bool CanWrite => false;

        public override long Length => inner.Length;

        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }

        /// <summary>
        /// 读取。
        /// </summary>
        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = inner.Read(buffer, offset, limiter.ReadSize(count));
            limiter.Wait(read, operation.Token).GetAwaiter().GetResult();
            operation.AddTransferredBytes(read);
            return read;
        }

        /// <summary>
        /// 读取。
        /// </summary>
        public override int Read(Span<byte> buffer)
        {
            var read = inner.Read(buffer[..limiter.ReadSize(buffer.Length)]);
            limiter.Wait(read, operation.Token).GetAwaiter().GetResult();
            operation.AddTransferredBytes(read);
            return read;
        }

        /// <summary>
        /// 读取。
        /// </summary>
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
        {
            var read = await inner.ReadAsync(buffer[..limiter.ReadSize(buffer.Length)], token);
            await limiter.Wait(read, token);
            operation.AddTransferredBytes(read);
            return read;
        }

        /// <summary>
        /// 读取。
        /// </summary>
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token) => await ReadAsync(buffer.AsMemory(offset, count), token);
        /// <summary>
        /// 执行 Seek。
        /// </summary>
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        /// <summary>
        /// 执行 Flush。
        /// </summary>
        public override void Flush() => inner.Flush();
        /// <summary>
        /// 设置Length。
        /// </summary>
        public override void SetLength(long value) => throw new NotSupportedException();
        /// <summary>
        /// 写入。
        /// </summary>
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        /// <summary>
        /// 执行 Dispose。
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                inner.Dispose();
            base.Dispose(disposing);
        }

        /// <summary>
        /// 执行 Dispose。
        /// </summary>
        public override async ValueTask DisposeAsync()
        {
            await inner.DisposeAsync();
            GC.SuppressFinalize(this);
        }
    }

    sealed class TransferLimiter(Func<int>? limit)
    {
        readonly object gate = new();
        double next;

        /// <summary>
        /// 读取Size。
        /// </summary>
        public int ReadSize(int requested)
        {
            var rate = limit?.Invoke() ?? 0;
            return rate <= 0 ? requested : Math.Min(requested, Math.Max(1, (int)Math.Min(81920, rate * 256L)));
        }

        /// <summary>
        /// 执行 Wait。
        /// </summary>
        public async Task Wait(int bytes, CancellationToken token)
        {
            var rate = limit?.Invoke() ?? 0;
            if (bytes <= 0 || rate <= 0)
                return;
            double delay;
            lock (gate)
            {
                var now = System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency;
                next = Math.Max(now, next) + bytes / (rate * 1024d);
                delay = Math.Max(0, next - now);
            }

            await Task.Delay(TimeSpan.FromSeconds(delay), token).ConfigureAwait(false);
        }
    }
}
