using System.Security.Claims;
using Farm.Mind.Data;

namespace Farm.Mind.Mcp;

public class McpUsageMiddleware(RequestDelegate next, ILogger<McpUsageMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, McpUsageStore usageStore)
    {
        if (!HttpMethods.IsPost(context.Request.Method) || context.Request.Path != "/mcp")
        {
            await next(context);
            return;
        }

        var subject = context.User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(subject))
        {
            await next(context);
            return;
        }

        var originalRequest = context.Request.Body;
        var originalResponse = context.Response.Body;
        var requestCounter = new CountingStream(originalRequest);
        var responseCounter = new CountingStream(originalResponse);
        context.Request.Body = requestCounter;
        context.Response.Body = responseCounter;

        try
        {
            await next(context);
        }
        finally
        {
            context.Request.Body = originalRequest;
            context.Response.Body = originalResponse;

            try
            {
                await usageStore.RecordAsync(subject, responseCounter.BytesWritten, requestCounter.BytesRead, CancellationToken.None);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Could not record aggregated MCP bandwidth usage.");
            }
        }
    }

    private sealed class CountingStream(Stream inner) : Stream
    {
        public long BytesRead { get; private set; }
        public long BytesWritten { get; private set; }
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = inner.Read(buffer, offset, count);
            BytesRead += read;
            return read;
        }

        public override int Read(Span<byte> buffer)
        {
            var read = inner.Read(buffer);
            BytesRead += read;
            return read;
        }

        public override int ReadByte()
        {
            var value = inner.ReadByte();
            if (value >= 0) BytesRead++;
            return value;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var read = await inner.ReadAsync(buffer, offset, count, cancellationToken);
            BytesRead += read;
            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = await inner.ReadAsync(buffer, cancellationToken);
            BytesRead += read;
            return read;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            inner.Write(buffer, offset, count);
            BytesWritten += count;
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            inner.Write(buffer);
            BytesWritten += buffer.Length;
        }

        public override void WriteByte(byte value)
        {
            inner.WriteByte(value);
            BytesWritten++;
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await inner.WriteAsync(buffer, offset, count, cancellationToken);
            BytesWritten += count;
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await inner.WriteAsync(buffer, cancellationToken);
            BytesWritten += buffer.Length;
        }

        public override void Flush() => inner.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { }
        public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
