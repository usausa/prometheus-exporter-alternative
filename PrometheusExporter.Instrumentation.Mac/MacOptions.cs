namespace PrometheusExporter.Instrumentation.Mac;

internal sealed class MacOptions
{
    public string Host { get; set; } = default!;

    public int UpdateDuration { get; set; } = 1000;
}
