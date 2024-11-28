namespace PrometheusExporter.Instrumentation.Ble;

using PrometheusExporter.Abstractions;

using Windows.Devices.Bluetooth.Advertisement;

internal sealed class BleInstrumentation : IDisposable
{
    private readonly string host;

    private readonly int signalThreshold;

    private readonly int timeThreshold; // TODO TimeSpan?

    private readonly bool knownOnly;

    private readonly Dictionary<ulong, DeviceEntry> knownDevices;

    private readonly IMetric metric;

    //private readonly SortedDictionary<ulong, Device> detectedDevices = [];

    private readonly BluetoothLEAdvertisementWatcher watcher;

    // TODO
    public BleInstrumentation(IMetricManager manager, BleOptions options)
    {
        host = options.Host;
        signalThreshold = options.SignalThreshold;
        timeThreshold = options.TimeThreshold;
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

        // TODO
    }

    public void Update()
    {
        // TODO remove old entry
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    // TODO

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
