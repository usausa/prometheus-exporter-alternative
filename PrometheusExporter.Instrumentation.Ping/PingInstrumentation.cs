namespace PrometheusExporter.Instrumentation.Ping;

internal sealed class PingInstrumentation : IDisposable
{
    private readonly Timer timer;

    public PingInstrumentation()
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
