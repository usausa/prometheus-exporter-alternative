namespace PrometheusExporter.Instrumentation.WFWattch2;

using PrometheusExporter.Abstractions;

internal sealed class WFWattch2Instrumentation : IDisposable
{
    private readonly Timer timer;

    public WFWattch2Instrumentation(IMetricManager manager, WFWattch2Options options)
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
