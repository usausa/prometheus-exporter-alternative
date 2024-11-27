namespace PrometheusExporter.Instrumentation.Ping;

using PrometheusExporter.Abstractions;

internal sealed class PingInstrumentation : IDisposable
{
    private readonly Timer timer;

    public PingInstrumentation(IMetricManager manager, PingOptions options)
    {
        timer = new Timer(Update, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(8000));
    }

    public void Dispose()
    {
        timer.Dispose();
    }

    private void Update(object? state)
    {
        // TODO
    }
}
