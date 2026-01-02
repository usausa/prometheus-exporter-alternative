namespace PrometheusExporter.Instrumentation.Wifi;

#pragma warning disable CA1819
public sealed class WifiOptions
{
    public int UpdateDuration { get; set; } = 1000;

    public int SignalThreshold { get; set; } = -75;

    public bool KnownOnly { get; set; }

    public string[] KnownAccessPoint { get; set; } = [];
}
#pragma warning restore CA1819
