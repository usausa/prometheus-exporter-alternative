namespace PrometheusExporter.Instrumentation.DiskInfo;

public sealed class DiskInfoOptions
{
    public int UpdateDuration { get; set; } = 10000;

    public string Host { get; set; } = default!;
}
