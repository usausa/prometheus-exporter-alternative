namespace PrometheusExporter.Instrumentation.DiskInfo;

public sealed class DiskInfoOptions
{
    public string Host { get; set; } = default!;

    public int UpdateDuration { get; set; } = 10000;
}
