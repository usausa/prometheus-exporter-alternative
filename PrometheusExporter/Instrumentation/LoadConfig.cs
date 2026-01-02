namespace PrometheusExporter.Instrumentation;

internal sealed class LoadConfig
{
    public string? Host { get; set; }

    public string[] Enable { get; set; } = default!;
}
