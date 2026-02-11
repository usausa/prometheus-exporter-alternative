namespace PrometheusExporter.Instrumentation.Ble;

using PrometheusExporter.Abstractions;

using PrometheusExporter.Infrastructure.Linux.BlueZ;

internal sealed class BleInstrumentation : IAsyncDisposable
{
    private readonly string host;

    private readonly int signalThreshold;

    private readonly bool knownOnly;

    private readonly Dictionary<string, DeviceEntry> knownDevices;

    private readonly IMetric metric;

    private readonly Lock sync = new();

    private readonly List<Device> devices = [];

    private readonly BleScanSession session;

    public BleInstrumentation(
        BleOptions options,
        IInstrumentationEnvironment environment,
        IMetricManager manager)
    {
        host = environment.Host;
        signalThreshold = options.SignalThreshold;
        knownOnly = options.KnownOnly;
        knownDevices = options.KnownDevice.ToDictionary(static x => x.Address);

        metric = manager.CreateGauge("ble_rssi");

#pragma warning disable CA2012
        session = BleScanSession.CreateAsync().GetAwaiter().GetResult();
#pragma warning restore CA2012
        session.DeviceEvent += OnDeviceEvent;
        _ = session.StartAsync();
    }

    public ValueTask DisposeAsync()
    {
        return session.DisposeAsync();
    }

    //--------------------------------------------------------------------------------
    // Event
    //--------------------------------------------------------------------------------

    private void OnDeviceEvent(BleScanEvent args)
    {
        if (args.Type == BleScanEventType.Lost)
        {
            lock (sync)
            {
                for (var i = devices.Count - 1; i >= 0; i--)
                {
                    var device = devices[i];
                    if (device.Path == args.DevicePath)
                    {
                        devices.RemoveAt(i);
                        device.Remove();
                        break;
                    }
                }
            }
        }
        else
        {
            if ((args.Rssi is null) || (args.Rssi.Value < signalThreshold))
            {
                return;
            }

            lock (sync)
            {
                var device = default(Device);
                foreach (var d in devices)
                {
                    if (d.Path == args.DevicePath)
                    {
                        device = d;
                        break;
                    }
                }

                if (device is null)
                {
                    var entry = (args.Address is not null) ? knownDevices.GetValueOrDefault(args.Address) : null;
                    if (knownOnly && (entry is null))
                    {
                        return;
                    }

                    device = new Device(args.DevicePath, metric.Create(MakeTags(args.Address!, args.Alias, entry)));
                    devices.Add(device);
                }

                device.Rssi.Value = args.Rssi.Value;
            }
        }
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private KeyValuePair<string, object?>[] MakeTags(string address, string? alias, DeviceEntry? device)
    {
        var name = device?.Name ?? alias ?? $"{address}";
        return [new("host", host), new("address", address), new("name", name)];
    }

    //--------------------------------------------------------------------------------
    // Device
    //--------------------------------------------------------------------------------

    private sealed class Device
    {
        public string Path { get; }

        public IMetricSeries Rssi { get; }

        public Device(string path, IMetricSeries rssi)
        {
            Path = path;
            Rssi = rssi;
        }

        public void Remove()
        {
            Rssi.Remove();
        }
    }
}
