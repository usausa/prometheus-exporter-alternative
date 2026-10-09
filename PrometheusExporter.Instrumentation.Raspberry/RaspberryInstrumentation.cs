namespace PrometheusExporter.Instrumentation.Raspberry;

using PrometheusExporter.Abstractions;

using RaspberryDotNet.SystemInfo;

internal sealed class RaspberryInstrumentation : IDisposable
{
    private readonly string host;

    private readonly TimeSpan updateDuration;

    private readonly List<Action> updateEntries = [];

    private readonly List<IDisposable> disposables = [];

    private DateTime lastUpdate;

    public RaspberryInstrumentation(
        RaspberryOptions options,
        IInstrumentationEnvironment environment,
        IMetricManager manager)
    {
        host = environment.Host;
        updateDuration = TimeSpan.FromMilliseconds(options.UpdateDuration);

        if (options.Vcio)
        {
            var vcio = Own(PlatformProvider.GetVcioMonitor());
            if (vcio.Supported)
            {
                updateEntries.Add(() => vcio.Update());
                SetupVcioTemperatureMetric(manager, vcio);
                SetupVcioFrequencyMetric(manager, vcio);
                SetupVcioVoltageMetric(manager, vcio);
                SetupVcioThrottledMetric(manager, vcio);
            }
        }

        if (options.Gpio)
        {
            var gpio = Own(PlatformProvider.GetGpioMonitor());
            if (gpio.Supported)
            {
                updateEntries.Add(() => gpio.Update());
                SetupGpioLevelMetric(manager, gpio);
            }
        }

        manager.AddBeforeCollectCallback(Update);
    }

    public void Dispose()
    {
        foreach (var resource in disposables)
        {
            resource.Dispose();
        }

        disposables.Clear();
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

    private T Own<T>(T resource)
        where T : IDisposable
    {
        disposables.Add(resource);
        return resource;
    }

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

    private void SetupVcioTemperatureMetric(IMetricManager manager, VcioMonitor vcio)
    {
        var metric = manager.CreateGauge("hardware_vcio_temperature");
        updateEntries.Add(MakeEntry(() => vcio.Temperature, metric.Create(MakeTags())));
    }

    //--------------------------------------------------------------------------------
    // Frequency
    //--------------------------------------------------------------------------------

    private void SetupVcioFrequencyMetric(IMetricManager manager, VcioMonitor vcio)
    {
        var metric = manager.CreateGauge("hardware_vcio_frequency");

        foreach (var clock in vcio.Clocks)
        {
#pragma warning disable CA1308
            var name = clock.Type.ToString().ToLowerInvariant();
#pragma warning restore CA1308
            updateEntries.Add(MakeEntry(() => clock.Frequency, metric.Create(MakeTags([new("name", name)]))));
        }
    }

    //--------------------------------------------------------------------------------
    // Voltage
    //--------------------------------------------------------------------------------

    private void SetupVcioVoltageMetric(IMetricManager manager, VcioMonitor vcio)
    {
        var metric = manager.CreateGauge("hardware_vcio_voltage");

        foreach (var voltage in vcio.Voltages)
        {
#pragma warning disable CA1308
            var name = voltage.Type.ToString().ToLowerInvariant();
#pragma warning restore CA1308
            updateEntries.Add(MakeEntry(() => voltage.Voltage, metric.Create(MakeTags([new("name", name)]))));
        }
    }

    //--------------------------------------------------------------------------------
    // Throttled
    //--------------------------------------------------------------------------------

    private void SetupVcioThrottledMetric(IMetricManager manager, VcioMonitor vcio)
    {
        var metric = manager.CreateGauge("hardware_vcio_throttled");

        var gaugeUnderVoltage = metric.Create(MakeTags([new("name", "under_voltage")]));
        var gaugeFrequencyCapped = metric.Create(MakeTags([new("name", "freq_cap")]));
        var gaugeCurrentlyThrottled = metric.Create(MakeTags([new("name", "throttled")]));
        var gaugeSoftTemperatureLimitActive = metric.Create(MakeTags([new("name", "temp_limit")]));

        updateEntries.Add(() =>
        {
            var throttled = vcio.Throttled;
            gaugeUnderVoltage.Value = (throttled & ThrottledFlags.UnderVoltageDetected) != 0 ? 1 : 0;
            gaugeFrequencyCapped.Value = (throttled & ThrottledFlags.ArmFrequencyCapped) != 0 ? 1 : 0;
            gaugeCurrentlyThrottled.Value = (throttled & ThrottledFlags.CurrentlyThrottled) != 0 ? 1 : 0;
            gaugeSoftTemperatureLimitActive.Value = (throttled & ThrottledFlags.SoftTemperatureLimitActive) != 0 ? 1 : 0;
        });
    }

    //--------------------------------------------------------------------------------
    // GPIO
    //--------------------------------------------------------------------------------

    private void SetupGpioLevelMetric(IMetricManager manager, GpioMonitor gpio)
    {
        var metric = manager.CreateGauge("hardware_gpio_level");

        foreach (var pin in gpio.Pins)
        {
            updateEntries.Add(MakeEntry(() => pin.Level, metric.Create(MakeTags([new("name", pin.PhysicalPin)]))));
        }
    }
}
