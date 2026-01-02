namespace PrometheusExporter.Instrumentation.Ping;

internal sealed class TargetEntry
{
    public string Address { get; set; } = default!;

    public string? Name { get; set; }
}

#pragma warning disable CA1819
internal sealed class PingOptions
{
    public int Interval { get; set; } = 10000;

    public int Timeout { get; set; } = 5000;

    public TargetEntry[] Target { get; set; } = default!;
}
#pragma warning restore CA1819
