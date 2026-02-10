namespace PrometheusExporter.Instrumentation.Ble;

internal sealed class DeviceEntry
{
    public string Address { get; set; } = default!;

    public string? Name { get; set; }
}

#pragma warning disable CA1819
internal sealed class BleOptions
{
    public int SignalThreshold { get; set; } = -127;

    public bool KnownOnly { get; set; }

    public DeviceEntry[] KnownDevice { get; set; } = [];
}
#pragma warning restore CA1819
