namespace PrometheusExporter.Instrumentation.HyperV;

#pragma warning disable CA1819
public sealed class HyperVOptions
{
    public string? IgnoreExpression { get; set; }

    public string Host { get; set; } = default!;
}
#pragma warning restore CA1819
