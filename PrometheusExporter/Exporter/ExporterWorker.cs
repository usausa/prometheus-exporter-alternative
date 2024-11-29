namespace PrometheusExporter.Exporter;

using System.Net;

using PrometheusExporter.Abstractions;
using PrometheusExporter.Metrics;

internal sealed class ExporterWorker : BackgroundService
{
    private readonly ILogger<ExporterWorker> logger;

    private readonly ExporterWorkerOptions options;

    private readonly IMetricManager manager;

    public ExporterWorker(
        ILogger<ExporterWorker> logger,
        IInstrumentationProvider provider,
        ExporterWorkerOptions options,
        IMetricManager manager)
    {
        this.logger = logger;
        this.options = options;
        this.manager = manager;

        provider.Setup();
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

            while (!stoppingToken.IsCancellationRequested)
            {
                var context = await listener.GetContextAsync().ConfigureAwait(false);
                _ = Task.Run(() => ProcessRequestAsync(context, stoppingToken), stoppingToken);
            }
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
            var timestamp = DateTimeOffset.UtcNow;

            using var buffer = new ResponseBuffer<byte>(65536);
            await manager.CollectAsync(buffer, timestamp.ToUnixTimeMilliseconds(), default!);

            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.Headers.Add("Last-Modified", timestamp.ToString("R"));
            context.Response.ContentType = "text/plain; charset=utf-8; version=0.0.4";

            context.Response.ContentLength64 = buffer.WrittenCount;
            await context.Response.OutputStream.WriteAsync(buffer.WrittenMemory, stoppingToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.ErrorExportFailed(ex);

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
}
