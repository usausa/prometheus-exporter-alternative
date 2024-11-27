namespace PrometheusExporter.Instrumentation.PerformanceCounter;
#pragma warning disable CA1819
public sealed class CounterEntry
{
    public string Name { get; set; } = default!;

    public string Category { get; set; } = default!;

    public string Counter { get; set; } = default!;

    public string? Instance { get; set; }

    public string[] InstanceIgnore { get; set; } = default!;
}
#pragma warning restore CA1819

#pragma warning disable CA1819
public sealed class PerformanceCounterOptions
{
    public int UpdateDuration { get; set; } = 1000;

    public string Prefix { get; set; } = "performance";

    public string Host { get; set; } = default!;

    public CounterEntry[] Counter { get; set; } = default!;
}
#pragma warning restore CA1819
