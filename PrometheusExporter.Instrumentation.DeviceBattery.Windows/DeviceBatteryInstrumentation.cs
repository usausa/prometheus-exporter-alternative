namespace PrometheusExporter.Instrumentation.DeviceBattery;

using DeviceLib.Battery;
using DeviceLib.Battery.Bluetooth;
using DeviceLib.Battery.Logitech;

using PrometheusExporter.Abstractions;

internal sealed class DeviceBatteryInstrumentation : IDisposable
{
    private readonly string host;

    private readonly bool knownOnly;

    private readonly Dictionary<string, DeviceEntry> knownDevices;

    private readonly BatteryReader reader;

    private readonly IMetric levelMetric;

    private readonly IMetric chargingMetric;

    private readonly IMetric voltageMetric;

    private readonly IMetric connectedMetric;

    private readonly Dictionary<string, Device> devices = new(StringComparer.Ordinal);

    private readonly Timer timer;

    private volatile int isRunning;

    public DeviceBatteryInstrumentation(
        DeviceBatteryOptions options,
        IInstrumentationEnvironment environment,
        IMetricManager manager)
    {
        host = environment.Host;
        knownOnly = options.KnownOnly;
        knownDevices = options.KnownDevice.ToDictionary(static x => x.Id, StringComparer.Ordinal);

        var providers = new List<IBatteryProvider>();
        if (options.Logitech)
        {
            providers.Add(new LogitechProvider());
        }

        if (options.Bluetooth)
        {
            providers.Add(new BluetoothProvider { IncludeDisconnected = options.IncludeDisconnected });
        }

        reader = new BatteryReader(providers);

        levelMetric = manager.CreateGauge("device_battery_level");
        chargingMetric = manager.CreateGauge("device_battery_charging");
        voltageMetric = manager.CreateGauge("device_battery_voltage");
        connectedMetric = manager.CreateGauge("device_battery_connected");

        timer = new Timer(Update, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(options.Interval));
    }

    public void Dispose()
    {
        timer.Dispose();
    }

    // ReSharper disable once AsyncVoidMethod
    private async void Update(object? state)
    {
        if (Interlocked.CompareExchange(ref isRunning, 1, 0) != 0)
        {
            return;
        }

#pragma warning disable CA1031
        try
        {
            Apply(await reader.ReadAsync().ConfigureAwait(false));
        }
        catch
        {
            // Ignore
        }
        finally
        {
            Interlocked.Exchange(ref isRunning, 0);
        }
#pragma warning restore CA1031
    }

    private void Apply(IEnumerable<BatteryInfo> batteries)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var battery in batteries)
        {
            var entry = knownDevices.GetValueOrDefault(battery.Id);
            if (knownOnly && (entry is null))
            {
                continue;
            }

            if (!devices.TryGetValue(battery.Id, out var device))
            {
                var tags = MakeTags(host, battery, entry?.Name ?? battery.Name);
                device = new Device(levelMetric.Create(tags), chargingMetric.Create(tags), voltageMetric.Create(tags), connectedMetric.Create(tags));
                devices.Add(battery.Id, device);
            }

            device.Update(battery);
            seen.Add(battery.Id);
        }

        foreach (var id in devices.Keys.Where(x => !seen.Contains(x)).ToList())
        {
            devices[id].Remove();
            devices.Remove(id);
        }
    }

    private static KeyValuePair<string, object?>[] MakeTags(string host, BatteryInfo battery, string name) =>
        [new("host", host), new("id", battery.Id), new("name", name), new("kind", battery.Kind.ToString()), new("connection", battery.Connection.ToString()), new("source", battery.Source)];

    private sealed class Device
    {
        private readonly IMetricSeries level;

        private readonly IMetricSeries charging;

        private readonly IMetricSeries voltage;

        private readonly IMetricSeries connected;

        public Device(IMetricSeries level, IMetricSeries charging, IMetricSeries voltage, IMetricSeries connected)
        {
            this.level = level;
            this.charging = charging;
            this.voltage = voltage;
            this.connected = connected;
        }

        public void Update(BatteryInfo battery)
        {
            level.Value = battery.Percent ?? double.NaN;
            charging.Value = battery.Status switch
            {
                BatteryStatus.Charging => 1,
                BatteryStatus.Discharging or BatteryStatus.Full => 0,
                _ => double.NaN
            };
            voltage.Value = battery.Voltage is { } millivolts ? millivolts / 1000.0 : double.NaN;
            connected.Value = battery.Connected ? 1 : 0;
        }

        public void Remove()
        {
            level.Remove();
            charging.Remove();
            voltage.Remove();
            connected.Remove();
        }
    }
}
