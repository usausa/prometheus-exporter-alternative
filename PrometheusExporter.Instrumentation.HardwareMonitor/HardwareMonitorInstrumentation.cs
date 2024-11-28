namespace PrometheusExporter.Instrumentation.HardwareMonitor;

using LibreHardwareMonitor.Hardware;

using PrometheusExporter.Abstractions;

internal sealed class HardwareMonitorInstrumentation : IDisposable
{
    private readonly TimeSpan updateDuration;

    private readonly Computer computer;

    private readonly UpdateVisitor updateVisitor = new();

    private readonly List<Entry> entries = [];

    private DateTime lastUpdate;

    // TODO
    public HardwareMonitorInstrumentation(IMetricManager manager, HardwareMonitorOptions options)
    {
        updateDuration = TimeSpan.FromMilliseconds(options.UpdateDuration);

        computer = new Computer
        {
            IsBatteryEnabled = options.IsBatteryEnabled,
            IsControllerEnabled = options.IsControllerEnabled,
            IsCpuEnabled = options.IsCpuEnabled,
            IsGpuEnabled = options.IsGpuEnabled,
            IsMemoryEnabled = options.IsMemoryEnabled,
            IsMotherboardEnabled = options.IsMotherboardEnabled,
            IsNetworkEnabled = options.IsNetworkEnabled,
            IsStorageEnabled = options.IsStorageEnabled
        };
        computer.Open();
        computer.Accept(updateVisitor);

        // TODO

        manager.AddBeforeCollectCallback(Update);
    }

    public void Dispose()
    {
        computer.Close();
    }

    //--------------------------------------------------------------------------------
    // Setup
    //--------------------------------------------------------------------------------

    // TODO

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

        computer.Accept(updateVisitor);
        foreach (var entry in entries)
        {
            entry.Update();
        }

        lastUpdate = now;
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    // TODO

    //--------------------------------------------------------------------------------
    // Entry
    //--------------------------------------------------------------------------------

    public sealed class Entry
    {
        private readonly ISensor sensor;

        private readonly Func<ISensor, double> measurement;

        private readonly IGauge gauge;

        public Entry(ISensor sensor, Func<ISensor, double> measurement, IGauge gauge)
        {
            this.sensor = sensor;
            this.measurement = measurement;
            this.gauge = gauge;
        }

        public void Update()
        {
            gauge.Value = measurement(sensor);
        }
    }
}
