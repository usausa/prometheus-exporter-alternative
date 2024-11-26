namespace PrometheusExporter.Instrumentation.Ble;

public sealed class DeviceEntry
{
    public string Address { get; set; } = default!;

    public string? Name { get; set; }
}

#pragma warning disable CA1819
public sealed class BleOptions
{
    public string Host { get; set; } = default!;

    public int SignalThreshold { get; set; } = -127;

    public int TimeThreshold { get; set; } = 60_000;

    public bool KnownOnly { get; set; }

    public DeviceEntry[] KnownDevice { get; set; } = [];
}
#pragma warning restore CA1819
