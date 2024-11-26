namespace PrometheusExporter.Exporter;

internal sealed class ExporterWorkerOptions
{
    public string EndPoint { get; set; } = default!;

    public string ScrapePath { get; set; } = "/metrics";
}
