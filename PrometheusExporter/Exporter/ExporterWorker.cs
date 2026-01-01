namespace PrometheusExporter.Exporter;

using System.Net;

using PrometheusExporter.Abstractions;
using PrometheusExporter.Instrumentation;

internal sealed class ExporterWorker : BackgroundService
{
    private readonly ILogger<ExporterWorker> log;

    private readonly ExporterWorkerOptions options;

    private readonly IMetricManager manager;

    public ExporterWorker(
        ILogger<ExporterWorker> log,
        IInstrumentationProvider provider,
        ExporterWorkerOptions options,
        IMetricManager manager)
    {
        this.log = log;
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
            log.InfoExporterStart();

            listener.Start();

            while (!stoppingToken.IsCancellationRequested)
            {
                var context = await listener.GetContextAsync().WaitAsync(stoppingToken).ConfigureAwait(false);
                _ = Task.Run(() => ProcessRequestAsync(context, stoppingToken), stoppingToken);
            }
        }
        finally
        {
            listener.Stop();

            log.InfoExporterStop();
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
            log.ErrorExportFailed(ex);

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
