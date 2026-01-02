namespace PrometheusExporter.Instrumentation.SystemControl;

public sealed class SystemControlOptions
{
    public string Host { get; set; } = default!;

    public int UpdateDuration { get; set; } = 1000;
}
