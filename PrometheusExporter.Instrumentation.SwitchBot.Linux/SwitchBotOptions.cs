namespace PrometheusExporter.Instrumentation.SwitchBot;

internal enum DeviceType
{
    Meter,
    PlugMini
}

internal sealed class DeviceEntry
{
    public DeviceType Type { get; set; }

    public string Address { get; set; } = default!;

    public string Name { get; set; } = default!;
}

#pragma warning disable CA1819
internal sealed class SwitchBotOptions
{
    public DeviceEntry[] Device { get; set; } = default!;
}
#pragma warning restore CA1819
