namespace PrometheusExporter.Instrumentation.HyperV;

#pragma warning disable CA1819
public sealed class HyperVOptions
{
    public int UpdateDuration { get; set; } = 1000;

    public string? IgnoreExpression { get; set; }

    public string Host { get; set; } = default!;
}
#pragma warning restore CA1819
