namespace PrometheusExporter.Instrumentation.HyperV;

using PrometheusExporter.Abstractions;

internal sealed class HyperVInstrumentation
{
    // TODO
    public HyperVInstrumentation(IMetricManager manager, HyperVOptions options)
    {
        manager.AddBeforeCollectCallback(Update);
    }

    //--------------------------------------------------------------------------------
    // Event
    //--------------------------------------------------------------------------------

    private void Update()
    {
    }
}
