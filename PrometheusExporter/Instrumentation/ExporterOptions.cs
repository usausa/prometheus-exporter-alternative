namespace PrometheusExporter.Instrumentation;

internal sealed class ExporterOptions
{
    public string Host { get; set; } = default!;

    public string[] InstrumentationList { get; set; } = default!;
}
