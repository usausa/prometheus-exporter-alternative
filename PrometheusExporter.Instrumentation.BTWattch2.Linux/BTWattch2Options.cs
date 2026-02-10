namespace PrometheusExporter.Instrumentation.BTWattch2;

internal sealed class DeviceEntry
{
    public string Address { get; set; } = default!;

    public string Name { get; set; } = default!;
}

#pragma warning disable CA1819
internal sealed class BTWattch2Options
{
    public DeviceEntry[] Device { get; set; } = default!;
}
#pragma warning restore CA1819
