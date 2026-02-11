namespace PrometheusExporter.Instrumentation.SwitchBot;

using System.Runtime.InteropServices.WindowsRuntime;

using PrometheusExporter.Abstractions;

using Windows.Devices.Bluetooth.Advertisement;

internal sealed class SwitchBotInstrumentation : IDisposable
{
    private readonly TimeSpan timeThreshold;

    private readonly Lock sync = new();

    private readonly Device[] devices;

    private readonly BluetoothLEAdvertisementWatcher watcher;

    public SwitchBotInstrumentation(
        SwitchBotOptions options,
        IMetricManager manager)
    {
        timeThreshold = TimeSpan.FromMilliseconds(options.TimeThreshold);

        var rssiMetric = manager.CreateGauge("sensor_rssi");
        var temperatureMetric = manager.CreateGauge("sensor_temperature");
        var humidityMetric = manager.CreateGauge("sensor_humidity");
        var co2Metric = manager.CreateGauge("sensor_co2");
        var powerMetric = manager.CreateGauge("sensor_power");

        var list = new List<Device>();
        foreach (var entry in options.Device)
        {
            var address = Convert.ToUInt64(entry.Address.Replace(":", string.Empty, StringComparison.Ordinal), 16);
            if (entry.Type == DeviceType.Meter)
            {
                var tags = MakeTags(entry.Address, entry.Name);
                list.Add(new MeterDevice(
                    address,
                    rssiMetric.Create(tags),
                    temperatureMetric.Create(tags),
                    humidityMetric.Create(tags),
                    co2Metric.Create(tags)));
            }
            else if (entry.Type == DeviceType.PlugMini)
            {
                var tags = MakeTags(entry.Address, entry.Name);
                list.Add(new PlugMiniDevice(
                    address,
                    rssiMetric.Create(tags),
                    powerMetric.Create(tags)));
            }
        }
        devices = list.ToArray();

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
        foreach (var md in args.Advertisement.ManufacturerData.Where(static x => x.CompanyId == 0x0969))
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

                if (device is MeterDevice meter)
                {
                    if (md.Data.Length >= 11)
                    {
                        var buffer = md.Data.ToArray();
                        meter.Temperature.Value = (((double)(buffer[8] & 0x0f) / 10) + (buffer[9] & 0x7f)) * ((buffer[9] & 0x80) > 0 ? 1 : -1);
                        meter.Humidity.Value = buffer[10] & 0x7f;
                        meter.Co2.Value = buffer.Length >= 16 ? (buffer[13] << 8) + buffer[14] : double.NaN;
                    }
                }
                else if (device is PlugMiniDevice plug)
                {
                    if (md.Data.Length >= 12)
                    {
                        var buffer = md.Data.ToArray();
                        plug.Power.Value = (double)(((buffer[10] & 0b00111111) << 8) + (buffer[11] & 0b01111111)) / 10;
                    }
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

    private static KeyValuePair<string, object?>[] MakeTags(string address, string name) =>
        [new("model", "switchbot"), new("address", address), new("name", name)];

    //--------------------------------------------------------------------------------
    // Device
    //--------------------------------------------------------------------------------

    private abstract class Device
    {
        public ulong Address { get; }

        public IGauge Rssi { get; }

        public DateTime LastUpdate { get; set; }

        protected Device(ulong address, IGauge rssi)
        {
            Address = address;
            Rssi = rssi;
        }

        public abstract void Clear();
    }

    private sealed class MeterDevice : Device
    {
        public IGauge Temperature { get; }

        public IGauge Humidity { get; }

        public IGauge Co2 { get; }

        public MeterDevice(ulong address, IGauge rssi, IGauge temperature, IGauge humidity, IGauge co2)
            : base(address, rssi)
        {
            Temperature = temperature;
            Humidity = humidity;
            Co2 = co2;
        }

        public override void Clear()
        {
            Rssi.Value = double.NaN;
            Temperature.Value = double.NaN;
            Humidity.Value = double.NaN;
            Co2.Value = double.NaN;
        }
    }

    private sealed class PlugMiniDevice : Device
    {
        public IGauge Power { get; }

        public PlugMiniDevice(ulong address, IGauge rssi, IGauge power)
            : base(address, rssi)
        {
            Power = power;
        }

        public override void Clear()
        {
            Rssi.Value = double.NaN;
            Power.Value = double.NaN;
        }
    }
}
