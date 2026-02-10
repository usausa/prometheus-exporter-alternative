namespace PrometheusExporter.Instrumentation.Ble;

using PrometheusExporter.Abstractions;

using PrometheusExporter.Infrastructure.Linux.BlueZ;

internal sealed class BleInstrumentation : IAsyncDisposable
{
    private readonly string host;

    private readonly int signalThreshold;

    private readonly TimeSpan timeThreshold;

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
        timeThreshold = TimeSpan.FromMilliseconds(options.TimeThreshold);
        knownOnly = options.KnownOnly;
        knownDevices = options.KnownDevice.ToDictionary(static x => x.Address);

        metric = manager.CreateGauge("ble_rssi");

        manager.AddBeforeCollectCallback(Update);

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
        if ((args.Address is null) || (args.Rssi is null) || (args.Rssi.Value < signalThreshold))
        {
            return;
        }

        lock (sync)
        {
            var device = default(Device);
            foreach (var d in devices)
            {
                if (d.Address == args.Address)
                {
                    device = d;
                    break;
                }
            }

            if (device is null)
            {
                var entry = knownDevices.GetValueOrDefault(args.Address);
                if (knownOnly && (entry is null))
                {
                    return;
                }

                device = new Device(args.Address, metric.CreateGauge(MakeTags(args.Address, args.Alias, entry)));
                devices.Add(device);
            }

            device.Rssi.Value = args.Rssi.Value;
            device.LastUpdate = DateTime.Now;
        }
    }

    private void Update()
    {
        lock (sync)
        {
            var now = DateTime.Now;
            for (var i = devices.Count - 1; i >= 0; i--)
            {
                var device = devices[i];
                if ((now - device.LastUpdate) > timeThreshold)
                {
                    devices.RemoveAt(i);
                    device.Remove();
                }
            }
        }
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private KeyValuePair<string, object?>[] MakeTags(string bluetoothAddress, string? alias, DeviceEntry? device)
    {
        var address = bluetoothAddress.Replace(":", string.Empty, StringComparison.Ordinal);
        var name = device?.Name ?? alias ?? $"({address})";
        return [new("host", host), new("address", address), new("name", name)];
    }

    //--------------------------------------------------------------------------------
    // Device
    //--------------------------------------------------------------------------------

    private sealed class Device
    {
        public string Address { get; }

        public DateTime LastUpdate { get; set; }

        public IGauge Rssi { get; }

        public Device(string address, IGauge rssi)
        {
            Address = address;
            Rssi = rssi;
        }

        public void Remove()
        {
            Rssi.Remove();
        }
    }
}
