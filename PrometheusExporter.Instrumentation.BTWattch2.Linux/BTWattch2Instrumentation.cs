namespace PrometheusExporter.Instrumentation.BTWattch2;

using PrometheusExporter.Abstractions;
using PrometheusExporter.Infrastructure.Linux.BlueZ;

internal sealed class BTWattch2Instrumentation : IAsyncDisposable
{
    private readonly Lock sync = new();

    private readonly Device[] devices;

    private readonly BleScanSession session;

    public BTWattch2Instrumentation(
        BTWattch2Options options,
        IMetricManager manager)
    {
        var rssiMetric = manager.CreateGauge("sensor_rssi");
        var powerMetric = manager.CreateGauge("sensor_power");
        var currentMetric = manager.CreateGauge("sensor_current");
        var voltageMetric = manager.CreateGauge("sensor_voltage");

        devices = options.Device
            .Select(x =>
            {
                var tags = MakeTags(x);
                return new Device(
                    x.Address,
                    rssiMetric.CreateGauge(tags),
                    powerMetric.CreateGauge(tags),
                    currentMetric.CreateGauge(tags),
                    voltageMetric.CreateGauge(tags));
            })
            .ToArray();

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
                        break;
                    }
                }
            }
        }
        else
        {
            lock (sync)
            {
            }
            //    foreach (var md in args.Advertisement.ManufacturerData.Where(static x => x.CompanyId == 0x0B60))
            //    {
            //        lock (sync)
            //        {
            //            var device = devices.FirstOrDefault(x => x.Address == args.BluetoothAddress);
            //            if (device is null)
            //            {
            //                return;
            //            }

            //            device.LastUpdate = DateTime.Now;
            //            device.Rssi.Value = args.RawSignalStrengthInDBm;

            //            if (md.Data.Length >= 8)
            //            {
            //                var buffer = md.Data.ToArray();
            //                device.Voltage.Value = (double)((buffer[2] << 8) + buffer[1]) / 10;
            //                device.Current.Value = (double)((buffer[4] << 8) + buffer[3]) / 1000;
            //                device.Power.Value = (double)((buffer[7] << 16) + (buffer[6] << 8) + buffer[5]) / 1000;
            //            }
            //        }
            //    }
        }
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private static KeyValuePair<string, object?>[] MakeTags(DeviceEntry entry) =>
        [new("model", "btwatch2"), new("address", entry.Address), new("name", entry.Name)];

    //--------------------------------------------------------------------------------
    // Device
    //--------------------------------------------------------------------------------

    private sealed class Device
    {
        public string Path { get; }

        public IGauge Rssi { get; }

        public IGauge Power { get; }

        public IGauge Current { get; }

        public IGauge Voltage { get; }

        public Device(string path, IGauge rssi, IGauge power, IGauge current, IGauge voltage)
        {
            Path = path;
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
