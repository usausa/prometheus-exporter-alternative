namespace HttpServer;

using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Net;
using System.Text;

internal class WorkerOptions
{
    public string EndPoint { get; set; } = "http://+:15000/";

    public string ScrapePath { get; set; } = "/metrics";
}

#pragma warning disable CA1848
internal class Worker : BackgroundService
{
    private readonly ILogger<Worker> logger;

    private readonly WorkerOptions options;

    public Worker(ILogger<Worker> logger, WorkerOptions options)
    {
        this.logger = logger;
        this.options = options;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var path = options.ScrapePath;
        if (!path.StartsWith('/'))
        {
            path = $"/{path}";
        }
        if (!path.EndsWith('/'))
        {
            path = $"{path}/";
        }

        using var listener = new HttpListener();
        listener.Prefixes.Add($"{options.EndPoint.TrimEnd('/')}{path}");

#pragma warning disable CA1031
        try
        {
            listener.Start();

            logger.LogInformation("Listening for HTTP requests...");

            while (!stoppingToken.IsCancellationRequested)
            {
                var context = await listener.GetContextAsync().ConfigureAwait(false);
                _ = Task.Run(() => ProcessRequestAsync(context, stoppingToken), stoppingToken);
            }
        }
        catch (Exception ex)
        {
            // Delete
            logger.LogError(ex, "Execute failed.");
        }
        finally
        {
            listener.Stop();
        }
#pragma warning restore CA1031
    }

    private async Task ProcessRequestAsync(HttpListenerContext context, CancellationToken stoppingToken)
    {
#pragma warning disable CA1031
        try
        {
            using var response = CreateResponse();
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.Headers.Add("Last-Modified", response.CollectAt.ToString("R"));
            context.Response.ContentType = "text/plain; charset=utf-8; version=0.0.4";

            context.Response.ContentLength64 = response.Size;
            await context.Response.OutputStream.WriteAsync(response.Buffer, stoppingToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed export.");

            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        }
#pragma warning restore CA1031

#pragma warning disable CA1031
        try
        {
            context.Response.Close();
        }
        catch
        {
            // ignored
        }
#pragma warning restore CA1031
    }

    private static Response CreateResponse()
    {
        // TODO
        var utcNow = DateTimeOffset.UtcNow;
        var tick = utcNow.ToUnixTimeMilliseconds();

        var response = new Response(utcNow);

        response.Write("# TYPE dummy_service_uptime gauge\n");
        response.Write("dummy_service_uptime{otel_scope_name=\"AlternativeExporter\",host=\"Dummy\"} ");
        response.Write((long)(DateTime.Now - Process.GetCurrentProcess().StartTime).TotalSeconds);
        response.Write(" ");
        response.Write(tick);
        response.Write("\n");

        response.Write("# EOF\n");

        return response;
    }
}

internal sealed class Response : IDisposable
{
    private byte[] buffer;

    private int size;

    public Memory<byte> Buffer => buffer.AsMemory(0, size);

    public int Size => size;

    public DateTimeOffset CollectAt { get; }

    public Response(DateTimeOffset collectAt)
    {
        CollectAt = collectAt;
        buffer = ArrayPool<byte>.Shared.Rent(65536);
    }

    public void Dispose()
    {
        if (buffer.Length > 0)
        {
            ArrayPool<byte>.Shared.Return(buffer);
            buffer = [];
        }
    }

    public void Write(ReadOnlySpan<char> value)
    {
        // TODO
        var length = Encoding.UTF8.GetBytes(value, buffer.AsSpan(size));
        size += length;
    }

    public void Write(long value)
    {
        // TODO
        Utf8Formatter.TryFormat(value, buffer.AsSpan(size), out var written);
        size += written;
    }
}
