namespace PrometheusExporter.Instrumentation.Raspberry;

using PrometheusExporter.Abstractions;

using RaspberryDotNet.SystemInfo;

internal sealed class RaspberryInstrumentation : IDisposable
{
    private readonly string host;

    private readonly TimeSpan updateDuration;

    private readonly Vcio vcio;

    private readonly GpioMap gpio;

    private readonly List<Action> updateEntries = [];

    private DateTime lastUpdate;

    public RaspberryInstrumentation(
        RaspberryOptions options,
        IInstrumentationEnvironment environment,
        IMetricManager manager)
    {
        host = environment.Host;
        updateDuration = TimeSpan.FromMilliseconds(options.UpdateDuration);

        vcio = new Vcio();
        gpio = new GpioMap();

        if (options.Vcio)
        {
            vcio.Open();
            SetupVcioTemperatureMetric(manager);
            SetupVcioFrequencyMetric(manager);
            SetupVcioVoltageMetric(manager);
            SetupVcioThrottledMetric(manager);
        }

        if (options.Gpio)
        {
            gpio.Open();
            SetupGpioLevelMetric(manager);
        }

        manager.AddBeforeCollectCallback(Update);
    }

    public void Dispose()
    {
        vcio.Dispose();
        gpio.Dispose();
    }

    //--------------------------------------------------------------------------------
    // Event
    //--------------------------------------------------------------------------------

    private void Update()
    {
        var now = DateTime.Now;
        if ((now - lastUpdate) < updateDuration)
        {
            return;
        }

        foreach (var action in updateEntries)
        {
            action();
        }

        lastUpdate = now;
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private KeyValuePair<string, object?>[] MakeTags(params KeyValuePair<string, object?>[] options)
    {
        if (options.Length == 0)
        {
            return [new("host", host)];
        }

        var tags = new List<KeyValuePair<string, object?>>([new("host", host)]);
        tags.AddRange(options);
        return [.. tags];
    }

    private static Action MakeEntry(Func<double> measurement, IMetricSeries series)
    {
        return () => series.Value = measurement();
    }

    //--------------------------------------------------------------------------------
    // Temperature
    //--------------------------------------------------------------------------------

    private void SetupVcioTemperatureMetric(IMetricManager manager)
    {
        var metric = manager.CreateGauge("hardware_vcio_temperature");
        updateEntries.Add(MakeEntry(() => vcio.ReadTemperature(), metric.Create(MakeTags())));
    }

    //--------------------------------------------------------------------------------
    // Frequency
    //--------------------------------------------------------------------------------

    private void SetupVcioFrequencyMetric(IMetricManager manager)
    {
        var metric = manager.CreateGauge("hardware_vcio_frequency");

        foreach (var clock in Enum.GetValues<ClockType>())
        {
#pragma warning disable CA1308
            var name = clock.ToString().ToLowerInvariant();
#pragma warning restore CA1308
            updateEntries.Add(MakeEntry(() =>
            {
                var frequency = vcio.ReadFrequency(clock, measured: true);
                if (Double.IsNaN(frequency))
                {
                    frequency = vcio.ReadFrequency(clock, measured: false);
                }
                return frequency;
            }, metric.Create(MakeTags([new("name", name)]))));
        }
    }

    //--------------------------------------------------------------------------------
    // Voltage
    //--------------------------------------------------------------------------------

    private void SetupVcioVoltageMetric(IMetricManager manager)
    {
        var metric = manager.CreateGauge("hardware_vcio_voltage");

        foreach (var voltage in Enum.GetValues<VoltageType>())
        {
#pragma warning disable CA1308
            var name = voltage.ToString().ToLowerInvariant();
#pragma warning restore CA1308
            updateEntries.Add(MakeEntry(() => vcio.ReadVoltage(voltage), metric.Create(MakeTags([new("name", name)]))));
        }
    }

    //--------------------------------------------------------------------------------
    // Throttled
    //--------------------------------------------------------------------------------

    private void SetupVcioThrottledMetric(IMetricManager manager)
    {
        var metric = manager.CreateGauge("hardware_vcio_throttled");

        var gaugeUnderVoltage = metric.Create(MakeTags([new("name", "under_voltage")]));
        var gaugeFrequencyCapped = metric.Create(MakeTags([new("name", "freq_cap")]));
        var gaugeCurrentlyThrottled = metric.Create(MakeTags([new("name", "throttled")]));
        var gaugeSoftTemperatureLimitActive = metric.Create(MakeTags([new("name", "temp_limit")]));

        updateEntries.Add(() =>
        {
            var throttled = vcio.ReadThrottled();
            gaugeUnderVoltage.Value = (throttled & ThrottledFlags.UnderVoltageDetected) != 0 ? 1 : 0;
            gaugeFrequencyCapped.Value = (throttled & ThrottledFlags.ArmFrequencyCapped) != 0 ? 1 : 0;
            gaugeCurrentlyThrottled.Value = (throttled & ThrottledFlags.CurrentlyThrottled) != 0 ? 1 : 0;
            gaugeSoftTemperatureLimitActive.Value = (throttled & ThrottledFlags.SoftTemperatureLimitActive) != 0 ? 1 : 0;
        });
    }

    //--------------------------------------------------------------------------------
    // Throttled
    //--------------------------------------------------------------------------------

    private void SetupGpioLevelMetric(IMetricManager manager)
    {
        var metric = manager.CreateGauge("hardware_gpio_level");

        var gauges = new Dictionary<int, IMetricSeries>();

        updateEntries.Add(() =>
        {
            foreach (var pin in gpio.ReadHeaderGpioPins())
            {
                if (!gauges.TryGetValue(pin.PhysicalPin, out var gauge))
                {
                    gauge = metric.Create(MakeTags([new("name", pin.PhysicalPin)]));
                    gauges[pin.PhysicalPin] = gauge;
                }

                gauge.Value = pin.Level;
            }
        });
    }
}
