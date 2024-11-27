namespace PrometheusExporter.Instrumentation.WFWattch2;

using System;
using System.Net;

using DeviceLib.WFWattch2;

using PrometheusExporter.Abstractions;

internal sealed class WFWattch2Instrumentation : IDisposable
{
    private readonly Device[] devices;

    private readonly Timer timer;

    public WFWattch2Instrumentation(IMetricManager manager, WFWattch2Options options)
    {
        var powerMetric = manager.CreateMetric("sensor_power");
        var currentMetric = manager.CreateMetric("sensor_current");
        var voltageMetric = manager.CreateMetric("sensor_voltage");

        devices = options.Device
            .Select(x =>
            {
                var tags = new KeyValuePair<string, object?>[]
                {
                    new("model", "wfwatch2"),
                    new("address", x.Address),
                    new("name", x.Name)
                };
                return new Device(
                    powerMetric.CreateGauge(tags),
                    currentMetric.CreateGauge(tags),
                    voltageMetric.CreateGauge(tags),
                    IPAddress.Parse(x.Address));
            })
            .ToArray();

        timer = new Timer(Update, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(options.Interval));
    }

    public void Dispose()
    {
        timer.Dispose();
        foreach (var device in devices)
        {
            device.Dispose();
        }
    }

    // ReSharper disable once AsyncVoidMethod
    private async void Update(object? state)
    {
        await Task.WhenAll(devices.Select(static x => x.UpdateAsync()));
    }

    //--------------------------------------------------------------------------------
    // Device
    //--------------------------------------------------------------------------------

    private sealed class Device : IDisposable
    {
        private readonly IGauge power;
        private readonly IGauge current;
        private readonly IGauge voltage;

        private readonly WattchClient client;

        public Device(IGauge power, IGauge current, IGauge voltage, IPAddress address)
        {
            this.power = power;
            this.current = current;
            this.voltage = voltage;
            client = new WattchClient(address);

            ClearValues();
        }

        public void Dispose()
        {
            client.Dispose();
        }

#pragma warning disable CA1031
        public async Task UpdateAsync()
        {
            try
            {
                if (!client.IsConnected())
                {
                    await client.ConnectAsync();
                }

                var result = await client.UpdateAsync();
                if (result)
                {
                    ReadValues();
                }
                else
                {
                    ClearValues();
                }
            }
            catch
            {
                ClearValues();

                client.Close();
            }
        }
#pragma warning restore CA1031

        private void ReadValues()
        {
            power.Value = client.Power ?? double.NaN;
            voltage.Value = client.Voltage ?? double.NaN;
            current.Value = client.Current ?? double.NaN;
        }

        private void ClearValues()
        {
            power.Value = double.NaN;
            voltage.Value = double.NaN;
            current.Value = double.NaN;
        }
    }
}
