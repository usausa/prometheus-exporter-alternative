namespace PrometheusExporter.Instrumentation.SensorOmron;

using DeviceLib.SensorOmron;

using PrometheusExporter.Abstractions;

internal sealed class SensorOmronInstrumentation : IDisposable
{
    private readonly Sensor[] sensors;

    private readonly Timer timer;

    public SensorOmronInstrumentation(
        SensorOmronOptions options,
        IMetricManager manager)
    {
        var temperatureMetric = manager.CreateGauge("sensor_temperature");
        var humidityMetric = manager.CreateGauge("sensor_humidity");
        var lightMetric = manager.CreateGauge("sensor_light");
        var pressureMetric = manager.CreateGauge("sensor_pressure");
        var noiseMetric = manager.CreateGauge("sensor_noise");
        var discomfortMetric = manager.CreateGauge("sensor_discomfort");
        var heatMetric = manager.CreateGauge("sensor_heat");
        var etvocMetric = manager.CreateGauge("sensor_tvoc");
        var eco2Metric = manager.CreateGauge("sensor_co2");
        var seismicMetric = manager.CreateGauge("sensor_seismic");

        sensors = options.Sensor
            .Select(x =>
            {
                var tags = MakeTags(x);
                return new Sensor(
                    temperatureMetric.CreateGauge(tags),
                    humidityMetric.CreateGauge(tags),
                    lightMetric.CreateGauge(tags),
                    pressureMetric.CreateGauge(tags),
                    noiseMetric.CreateGauge(tags),
                    discomfortMetric.CreateGauge(tags),
                    heatMetric.CreateGauge(tags),
                    etvocMetric.CreateGauge(tags),
                    eco2Metric.CreateGauge(tags),
                    seismicMetric.CreateGauge(tags),
                    x.Port);
            })
            .ToArray();

        timer = new Timer(Update, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(options.Interval));
    }

    public void Dispose()
    {
        timer.Dispose();
        foreach (var sensor in sensors)
        {
            sensor.Dispose();
        }
    }

    //--------------------------------------------------------------------------------
    // Event
    //--------------------------------------------------------------------------------

    // ReSharper disable once AsyncVoidMethod
    private async void Update(object? state)
    {
        await Task.WhenAll(sensors.Select(static x => x.UpdateAsync())).ConfigureAwait(false);
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private static KeyValuePair<string, object?>[] MakeTags(SensorEntry entry) =>
        [new("model", "rbt"), new("address", entry.Port), new("name", entry.Name)];

    //--------------------------------------------------------------------------------
    // Sensor
    //--------------------------------------------------------------------------------

    private sealed class Sensor : IDisposable
    {
        private readonly IGauge temperature;
        private readonly IGauge humidity;
        private readonly IGauge light;
        private readonly IGauge pressure;
        private readonly IGauge noise;
        private readonly IGauge discomfort;
        private readonly IGauge heat;
        private readonly IGauge etvoc;
        private readonly IGauge eco2;
        private readonly IGauge seismic;

        private readonly RbtSensorSerial sensor;

        public Sensor(
            IGauge temperature,
            IGauge humidity,
            IGauge light,
            IGauge pressure,
            IGauge noise,
            IGauge discomfort,
            IGauge heat,
            IGauge etvoc,
            IGauge eco2,
            IGauge seismic,
            string port)
        {
            this.temperature = temperature;
            this.humidity = humidity;
            this.light = light;
            this.pressure = pressure;
            this.noise = noise;
            this.discomfort = discomfort;
            this.heat = heat;
            this.etvoc = etvoc;
            this.eco2 = eco2;
            this.seismic = seismic;

            sensor = new RbtSensorSerial(port);

            ClearValues();
        }

        public void Dispose()
        {
            sensor.Dispose();
        }

#pragma warning disable CA1031
        public async Task UpdateAsync()
        {
            try
            {
                if (!sensor.IsOpen())
                {
                    sensor.Open();
                }

                var result = await sensor.UpdateAsync().ConfigureAwait(false);
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

                sensor.Close();
            }
        }
#pragma warning restore CA1031

        private void ReadValues()
        {
            temperature.Value = sensor.Temperature ?? double.NaN;
            humidity.Value = sensor.Humidity ?? double.NaN;
            light.Value = sensor.Light ?? double.NaN;
            pressure.Value = sensor.Pressure ?? double.NaN;
            noise.Value = sensor.Noise ?? double.NaN;
            discomfort.Value = sensor.Discomfort ?? double.NaN;
            heat.Value = sensor.Heat ?? double.NaN;
            etvoc.Value = sensor.Etvoc ?? double.NaN;
            eco2.Value = sensor.Eco2 ?? double.NaN;
            seismic.Value = sensor.Seismic ?? double.NaN;
        }

        private void ClearValues()
        {
            temperature.Value = double.NaN;
            humidity.Value = double.NaN;
            light.Value = double.NaN;
            pressure.Value = double.NaN;
            noise.Value = double.NaN;
            discomfort.Value = double.NaN;
            heat.Value = double.NaN;
            etvoc.Value = double.NaN;
            eco2.Value = double.NaN;
            seismic.Value = double.NaN;
        }
    }
}
