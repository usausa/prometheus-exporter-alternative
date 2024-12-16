namespace PrometheusExporter.Instrumentation.Platform.Linux;

public sealed class PlatformLinuxOptions
{
    public string Host { get; set; } = default!;

    public int UpdateDuration { get; set; } = 1000;
}
