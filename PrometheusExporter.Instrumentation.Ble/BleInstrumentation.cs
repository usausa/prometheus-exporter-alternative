namespace PrometheusExporter.Instrumentation.Ble;

using PrometheusExporter.Abstractions;

using Windows.Devices.Bluetooth.Advertisement;

internal sealed class BleInstrumentation : IDisposable
{
    private readonly string host;

    private readonly int signalThreshold;

    private readonly TimeSpan timeThreshold;

    private readonly bool knownOnly;

    private readonly Dictionary<ulong, DeviceEntry> knownDevices;

    private readonly IMetric metric;

    private readonly Lock sync = new();

    private readonly List<Device> devices = [];

    private readonly BluetoothLEAdvertisementWatcher watcher;

    public BleInstrumentation(
        BleOptions options,
        IInstrumentationEnvironment environment,
        IMetricManager manager)
    {
        host = environment.Host;
        signalThreshold = options.SignalThreshold;
        timeThreshold = TimeSpan.FromMilliseconds(options.TimeThreshold);
        knownOnly = options.KnownOnly;
        knownDevices = options.KnownDevice.ToDictionary(static x => NormalizeAddress(x.Address));

        metric = manager.CreateMetric("ble_rssi");

        manager.AddBeforeCollectCallback(Update);

        watcher = new BluetoothLEAdvertisementWatcher
        {
            ScanningMode = BluetoothLEScanningMode.Active
        };
        watcher.Received += OnWatcherReceived;
        watcher.Start();
    }

    public void Dispose()
    {
        watcher.Stop();
    }

    //--------------------------------------------------------------------------------
    // Event
    //--------------------------------------------------------------------------------

    private void OnWatcherReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
    {
        if (args.RawSignalStrengthInDBm <= signalThreshold)
        {
            return;
        }

        lock (sync)
        {
            var device = default(Device);
            foreach (var d in devices)
            {
                if (d.Address == args.BluetoothAddress)
                {
                    device = d;
                    break;
                }
            }

            if (device is null)
            {
                var entry = knownDevices.GetValueOrDefault(args.BluetoothAddress);
                if (knownOnly && (entry is null))
                {
                    return;
                }

                device = new Device(args.BluetoothAddress, metric.CreateGauge(MakeTags(args.BluetoothAddress, entry)));
                devices.Add(device);
            }

            device.Rssi.Value = args.RawSignalStrengthInDBm;
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

    private static ulong NormalizeAddress(string address) =>
        Convert.ToUInt64(address.Replace(":", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal), 16);

    private KeyValuePair<string, object?>[] MakeTags(ulong bluetoothAddress, DeviceEntry? device)
    {
        var address = $"{bluetoothAddress:X12}";
        var name = device?.Name ?? $"({address})";
        return [new("host", host), new("address", address), new("name", name)];
    }

    //--------------------------------------------------------------------------------
    // Device
    //--------------------------------------------------------------------------------

    private sealed class Device
    {
        public ulong Address { get; }

        public DateTime LastUpdate { get; set; }

        public IGauge Rssi { get; }

        public Device(ulong address, IGauge rssi)
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
