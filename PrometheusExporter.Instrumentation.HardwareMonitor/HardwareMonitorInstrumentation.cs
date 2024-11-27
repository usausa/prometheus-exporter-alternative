namespace PrometheusExporter.Instrumentation.HardwareMonitor;

using PrometheusExporter.Abstractions;

internal sealed class HardwareMonitorInstrumentation
{
    // TODO
    public HardwareMonitorInstrumentation(IMetricManager manager, HardwareMonitorOptions options)
    {
        manager.AddBeforeCollectCallback(Update);
    }

    private void Update()
    {
    }
}
