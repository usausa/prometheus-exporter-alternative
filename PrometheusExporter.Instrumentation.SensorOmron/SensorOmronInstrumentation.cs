namespace PrometheusExporter.Instrumentation.SensorOmron;

using PrometheusExporter.Abstractions;

internal sealed class SensorOmronInstrumentation : IDisposable
{
    private readonly Timer timer;

    public SensorOmronInstrumentation(IMetricManager manager, SensorOmronOptions options)
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
