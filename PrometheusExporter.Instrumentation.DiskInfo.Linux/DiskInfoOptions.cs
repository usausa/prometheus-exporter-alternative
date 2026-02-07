namespace PrometheusExporter.Instrumentation.DiskInfo;

internal sealed class DiskInfoOptions
{
    public int UpdateDuration { get; set; } = 10000;
}
