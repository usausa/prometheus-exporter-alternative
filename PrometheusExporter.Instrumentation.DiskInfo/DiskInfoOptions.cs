namespace PrometheusExporter.Instrumentation.DiskInfo;

public sealed class DiskInfoOptions
{
    public int Interval { get; set; } = 300_0000;

    public string Host { get; set; } = default!;
}
