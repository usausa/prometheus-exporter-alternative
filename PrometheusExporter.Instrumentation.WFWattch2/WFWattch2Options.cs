namespace PrometheusExporter.Instrumentation.WFWattch2;

internal sealed class DeviceEntry
{
    public string Address { get; set; } = default!;

    public string Name { get; set; } = default!;
}

#pragma warning disable CA1819
internal sealed class WFWattch2Options
{
    public int Interval { get; set; } = 5000;

    public DeviceEntry[] Device { get; set; } = default!;
}
#pragma warning restore CA1819
