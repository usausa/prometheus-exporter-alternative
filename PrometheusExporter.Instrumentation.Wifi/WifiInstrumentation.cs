namespace PrometheusExporter.Instrumentation.Wifi;

using PrometheusExporter.Abstractions;

internal sealed class WifiInstrumentation
{
    // TODO
    public WifiInstrumentation(IMetricManager manager, WifiOptions options)
    {
        manager.AddBeforeCollectCallback(Update);
    }

    private void Update()
    {
    }
}
