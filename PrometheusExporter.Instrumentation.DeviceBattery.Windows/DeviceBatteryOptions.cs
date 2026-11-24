namespace PrometheusExporter.Instrumentation.DeviceBattery;

internal sealed class DeviceEntry
{
    public string Id { get; set; } = default!;

    public string? Name { get; set; }
}

#pragma warning disable CA1819
internal sealed class DeviceBatteryOptions
{
    public int Interval { get; set; } = 60_000;

    public bool Logitech { get; set; } = true;

    public bool Bluetooth { get; set; } = true;

    public bool IncludeDisconnected { get; set; }

    public bool KnownOnly { get; set; }

    public DeviceEntry[] KnownDevice { get; set; } = [];
}
#pragma warning restore CA1819
