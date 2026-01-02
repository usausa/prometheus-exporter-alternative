namespace PrometheusExporter.Instrumentation.HyperV;

#pragma warning disable CA1819
internal sealed class HyperVOptions
{
    public int UpdateDuration { get; set; } = 1000;

    public string? IgnoreExpression { get; set; }
}
#pragma warning restore CA1819
