namespace PrometheusExporter.Instrumentation.WFWattch2;

internal sealed class WFWattch2Instrumentation : IDisposable
{
    private readonly Timer timer;

    public WFWattch2Instrumentation()
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
