namespace PrometheusExporter.Instrumentation.SwitchBot;

using PrometheusExporter.Abstractions;
using PrometheusExporter.Infrastructure.Linux.BlueZ;

internal sealed class SwitchBotInstrumentation : IAsyncDisposable
{
    private readonly Lock sync = new();

    private readonly Device[] devices;

    private readonly BleScanSession session;

    public SwitchBotInstrumentation(
        SwitchBotOptions options,
        IMetricManager manager)
    {
        var rssiMetric = manager.CreateGauge("sensor_rssi");
        var temperatureMetric = manager.CreateGauge("sensor_temperature");
        var humidityMetric = manager.CreateGauge("sensor_humidity");
        var co2Metric = manager.CreateGauge("sensor_co2");
        var powerMetric = manager.CreateGauge("sensor_power");

        var list = new List<Device>();
        foreach (var entry in options.Device)
        {
            if (entry.Type == DeviceType.Meter)
            {
                var tags = MakeTags(entry.Address, entry.Name);
                list.Add(new MeterDevice(
                    entry.Address,
                    rssiMetric.CreateGauge(tags),
                    temperatureMetric.CreateGauge(tags),
                    humidityMetric.CreateGauge(tags),
                    co2Metric.CreateGauge(tags)));
            }
            else if (entry.Type == DeviceType.PlugMini)
            {
                var tags = MakeTags(entry.Address, entry.Name);
                list.Add(new PlugMiniDevice(
                    entry.Address,
                    rssiMetric.CreateGauge(tags),
                    powerMetric.CreateGauge(tags)));
            }
        }
        devices = list.ToArray();

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
                foreach (var device in devices)
                {
                    if (device.Path == args.DevicePath)
                    {
                        device.Clear();
                        device.Path = null;
                        break;
                    }
                }
            }
        }
        else
        {
            lock (sync)
            {
                var device = devices.FirstOrDefault(x => x.Path == args.DevicePath);
                if ((device is null) && (args.Address is not null))
                {
                    device = devices.FirstOrDefault(x => x.Address == args.Address);
                    device?.Path = args.DevicePath;
                }

                if (device is null)
                {
                    return;
                }

                if (args.Rssi.HasValue)
                {
                    device.Rssi.Value = args.Rssi.Value;
                }

                if ((args.ManufacturerData is null) || !args.ManufacturerData.TryGetValue(0x0969, out var buffer))
                {
                    return;
                }

                if (device is MeterDevice meter)
                {
                    if (buffer.Length >= 11)
                    {
                        meter.Temperature.Value = (((double)(buffer[8] & 0x0f) / 10) + (buffer[9] & 0x7f)) * ((buffer[9] & 0x80) > 0 ? 1 : -1);
                        meter.Humidity.Value = buffer[10] & 0x7f;
                        meter.Co2.Value = buffer.Length >= 16 ? (buffer[13] << 8) + buffer[14] : double.NaN;
                    }
                }
                else if (device is PlugMiniDevice plug)
                {
                    if (buffer.Length >= 12)
                    {
                        plug.Power.Value = (double)(((buffer[10] & 0b00111111) << 8) + (buffer[11] & 0b01111111)) / 10;
                    }
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
        public string? Path { get; set; }

        public string Address { get; }

        public IGauge Rssi { get; }

        protected Device(string address, IGauge rssi)
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

        public MeterDevice(string address, IGauge rssi, IGauge temperature, IGauge humidity, IGauge co2)
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

        public PlugMiniDevice(string address, IGauge rssi, IGauge power)
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
