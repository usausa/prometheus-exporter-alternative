namespace PrometheusExporter.Instrumentation.BTWattch2;

public sealed class DeviceEntry
{
    public string Address { get; set; } = default!;

    public string Name { get; set; } = default!;
}

#pragma warning disable CA1819
public sealed class BTWattch2Options
{
    public int TimeThreshold { get; set; } = 180_000;

    public DeviceEntry[] Device { get; set; } = default!;
}
#pragma warning restore CA1819
