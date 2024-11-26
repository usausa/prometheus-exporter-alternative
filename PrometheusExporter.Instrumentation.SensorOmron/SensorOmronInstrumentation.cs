namespace PrometheusExporter.Instrumentation.SensorOmron;

internal sealed class SensorOmronInstrumentation : IDisposable
{
    private readonly Timer timer;

    public SensorOmronInstrumentation()
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
