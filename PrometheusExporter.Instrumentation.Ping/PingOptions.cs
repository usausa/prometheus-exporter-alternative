namespace PrometheusExporter.Instrumentation.Ping;

public sealed class TargetEntry
{
    public string Address { get; set; } = default!;

    public string? Name { get; set; }
}

#pragma warning disable CA1819
public sealed class PingOptions
{
    public string Host { get; set; } = default!;

    public int Interval { get; set; } = 10000;

    public int Timeout { get; set; } = 5000;

    public TargetEntry[] Target { get; set; } = default!;
}
#pragma warning restore CA1819
