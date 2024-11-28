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

    private readonly object sync = new();

    private readonly List<Device> detectedDevices = [];

    private readonly BluetoothLEAdvertisementWatcher watcher;

    public BleInstrumentation(IMetricManager manager, BleOptions options)
    {
        host = options.Host;
        signalThreshold = options.SignalThreshold;
        timeThreshold = TimeSpan.FromMilliseconds(options.TimeThreshold);
        knownOnly = options.KnownOnly;
        knownDevices = options.KnownDevice
            .ToDictionary(static x => Convert.ToUInt64(x.Address.Replace(":", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal), 16));

        watcher = new BluetoothLEAdvertisementWatcher
        {
            ScanningMode = BluetoothLEScanningMode.Active
        };
        watcher.Received += OnWatcherReceived;
        watcher.Start();

        metric = manager.CreateMetric("ble_rssi");

        manager.AddBeforeCollectCallback(Update);
    }

    public void Dispose()
    {
        watcher.Stop();
        watcher.Received -= OnWatcherReceived;
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
            foreach (var detectedDevice in detectedDevices)
            {
                if (detectedDevice.Address == args.BluetoothAddress)
                {
                    device = detectedDevice;
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
                detectedDevices.Add(device);
            }

            device.Gauge.Value = args.RawSignalStrengthInDBm;
            device.LastUpdate = DateTime.Now;
        }
    }

    public void Update()
    {
        lock (sync)
        {
            var now = DateTime.Now;
            for (var i = detectedDevices.Count - 1; i >= 0; i--)
            {
                var device = detectedDevices[i];
                if ((now - device.LastUpdate) > timeThreshold)
                {
                    detectedDevices.RemoveAt(i);
                    device.Gauge.Remove();
                }
            }
        }
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

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

        public IGauge Gauge { get; }

        public Device(ulong address, IGauge gauge)
        {
            Address = address;
            Gauge = gauge;
        }
    }
}
