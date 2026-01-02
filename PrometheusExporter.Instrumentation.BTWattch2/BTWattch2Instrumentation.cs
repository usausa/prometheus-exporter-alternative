namespace PrometheusExporter.Instrumentation.BTWattch2;

using System.Runtime.InteropServices.WindowsRuntime;

using PrometheusExporter.Abstractions;

using Windows.Devices.Bluetooth.Advertisement;

internal sealed class BTWattch2Instrumentation : IDisposable
{
    private readonly TimeSpan timeThreshold;

    private readonly Lock sync = new();

    private readonly Device[] devices;

    private readonly BluetoothLEAdvertisementWatcher watcher;

    public BTWattch2Instrumentation(
        BTWattch2Options options,
        IMetricManager manager)
    {
        timeThreshold = TimeSpan.FromMilliseconds(options.TimeThreshold);

        var rssiMetric = manager.CreateMetric("sensor_rssi");
        var powerMetric = manager.CreateMetric("sensor_power");
        var currentMetric = manager.CreateMetric("sensor_current");
        var voltageMetric = manager.CreateMetric("sensor_voltage");

        devices = options.Device
            .Select(x =>
            {
                var tags = MakeTags(x);
                return new Device(
                    NormalizeAddress(x.Address),
                    rssiMetric.CreateGauge(tags),
                    powerMetric.CreateGauge(tags),
                    currentMetric.CreateGauge(tags),
                    voltageMetric.CreateGauge(tags));
            })
            .ToArray();

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
        foreach (var md in args.Advertisement.ManufacturerData.Where(static x => x.CompanyId == 0x0B60))
        {
            lock (sync)
            {
                var device = devices.FirstOrDefault(x => x.Address == args.BluetoothAddress);
                if (device is null)
                {
                    return;
                }

                device.LastUpdate = DateTime.Now;
                device.Rssi.Value = args.RawSignalStrengthInDBm;

                if (md.Data.Length >= 8)
                {
                    var buffer = md.Data.ToArray();
                    device.Voltage.Value = (double)((buffer[2] << 8) + buffer[1]) / 10;
                    device.Current.Value = (double)((buffer[4] << 8) + buffer[3]) / 1000;
                    device.Power.Value = (double)((buffer[7] << 16) + (buffer[6] << 8) + buffer[5]) / 1000;
                }
            }
        }
    }

    private void Update()
    {
        lock (sync)
        {
            var now = DateTime.Now;
            foreach (var device in devices)
            {
                if ((now - device.LastUpdate) > timeThreshold)
                {
                    device.Clear();
                }
            }
        }
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private static ulong NormalizeAddress(string address) =>
        Convert.ToUInt64(
            address.Replace(":", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal), 16);

    private static KeyValuePair<string, object?>[] MakeTags(DeviceEntry entry) =>
        [new("model", "btwatch2"), new("address", entry.Address), new("name", entry.Name)];

    //--------------------------------------------------------------------------------
    // Device
    //--------------------------------------------------------------------------------

    private sealed class Device
    {
        public ulong Address { get; }

        public IGauge Rssi { get; }

        public IGauge Power { get; }

        public IGauge Current { get; }

        public IGauge Voltage { get; }

        public DateTime LastUpdate { get; set; }

        public Device(ulong address, IGauge rssi, IGauge power, IGauge current, IGauge voltage)
        {
            Address = address;
            Rssi = rssi;
            Power = power;
            Current = current;
            Voltage = voltage;
        }

        public void Clear()
        {
            Rssi.Value = double.NaN;
            Power.Value = double.NaN;
            Current.Value = double.NaN;
            Voltage.Value = double.NaN;
        }
    }
}
