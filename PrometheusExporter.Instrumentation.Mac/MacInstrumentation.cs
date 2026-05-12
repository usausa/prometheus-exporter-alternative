namespace PrometheusExporter.Instrumentation.Mac;

using PrometheusExporter.Abstractions;

using MacDotNet.SystemInfo;

internal sealed class MacInstrumentation
{
    private readonly string host;

    private readonly TimeSpan updateDuration;

    private readonly List<Action> prepareEntries = [];

    private readonly List<Action> updateEntries = [];

    private DateTime lastUpdate;

    public MacInstrumentation(
        MacOptions options,
        IInstrumentationEnvironment environment,
        IMetricManager manager)
    {
        host = environment.Host;
        updateDuration = TimeSpan.FromMilliseconds(options.UpdateDuration);

        if (options.Uptime)
        {
            SetupUptimeMetric(manager);
        }
        if (options.Cpu)
        {
            SetupCpuMetric(manager);
        }
        if (options.LoadAverage)
        {
            SetupLoadAverageMetric(manager);
        }
        if (options.Memory)
        {
            SetupMemoryMetric(manager);
        }
        if (options.SwapUsage)
        {
            SetupSwapUsageMetric(manager);
        }
        if (options.FileSystem)
        {
            SetupFileSystemMetric(manager);
        }
        if (options.Disk)
        {
            SetupDiskMetric(manager);
        }
        if (options.FileDescriptor)
        {
            SetupFileDescriptorMetric(manager);
        }
        if (options.Network)
        {
            SetupNetworkMetric(manager);
        }
        if (options.ProcessSummary)
        {
            SetupProcessSummaryMetric(manager);
        }
        if (options.CpuFrequency)
        {
            SetupCpuFrequencyMetric(manager);
        }
        if (options.Gpu)
        {
            SetupGpuMetric(manager);
        }
        if (options.Power)
        {
            SetupPowerMetric(manager);
        }
        if (options.HardwareMonitor)
        {
            SetupHardwareMonitorMetric(manager);
        }

        manager.AddBeforeCollectCallback(Update);
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

        foreach (var action in prepareEntries)
        {
            action();
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

    //private static bool IsTarget(IEnumerable<string> targets, string name) =>
    //    targets.Any(x => (x == "*") || (x == name));

    private static Action MakeEntry(Func<double> measurement, IMetricSeries series)
    {
        return () => series.Value = measurement();
    }

    //--------------------------------------------------------------------------------
    // Uptime
    //--------------------------------------------------------------------------------

    private void SetupUptimeMetric(IMetricManager manager)
    {
        // Uptime
        var uptimeInfo = PlatformProvider.GetUptime();

        prepareEntries.Add(() => uptimeInfo.Update());

        var metric = manager.CreateGauge("system_uptime");
        updateEntries.Add(MakeEntry(() => uptimeInfo.Elapsed.TotalSeconds, metric.Create(MakeTags())));
    }

    //--------------------------------------------------------------------------------
    // Cpu
    //--------------------------------------------------------------------------------

    private void SetupCpuMetric(IMetricManager manager)
    {
        // TODO
    }

    //--------------------------------------------------------------------------------
    // LoadAverage
    //--------------------------------------------------------------------------------

    private void SetupLoadAverageMetric(IMetricManager manager)
    {
        var load = PlatformProvider.GetLoadAverage();

        prepareEntries.Add(() => load.Update());

        var metric = manager.CreateGauge("system_load_average");
        updateEntries.Add(MakeEntry(() => load.Average1, metric.Create(MakeTags([new("window", 1)]))));
        updateEntries.Add(MakeEntry(() => load.Average5, metric.Create(MakeTags([new("window", 5)]))));
        updateEntries.Add(MakeEntry(() => load.Average15, metric.Create(MakeTags([new("window", 15)]))));
    }

    //--------------------------------------------------------------------------------
    // Memory
    //--------------------------------------------------------------------------------

    private void SetupMemoryMetric(IMetricManager manager)
    {
        // TODO
    }

    //--------------------------------------------------------------------------------
    // SwapUsage
    //--------------------------------------------------------------------------------

    private void SetupSwapUsageMetric(IMetricManager manager)
    {
        // TODO
    }

    //--------------------------------------------------------------------------------
    // FileSystem
    //--------------------------------------------------------------------------------

    private void SetupFileSystemMetric(IMetricManager manager)
    {
        // TODO
    }

    //--------------------------------------------------------------------------------
    // Disk
    //--------------------------------------------------------------------------------

    private void SetupDiskMetric(IMetricManager manager)
    {
        // TODO
    }

    //--------------------------------------------------------------------------------
    // FileDescriptor
    //--------------------------------------------------------------------------------

    private void SetupFileDescriptorMetric(IMetricManager manager)
    {
        // TODO
    }

    //--------------------------------------------------------------------------------
    // Network
    //--------------------------------------------------------------------------------

    private void SetupNetworkMetric(IMetricManager manager)
    {
        // TODO
    }

    //--------------------------------------------------------------------------------
    // ProcessSummary
    //--------------------------------------------------------------------------------

    private void SetupProcessSummaryMetric(IMetricManager manager)
    {
        // TODO
    }

    //--------------------------------------------------------------------------------
    // CpuFrequency
    //--------------------------------------------------------------------------------

    private void SetupCpuFrequencyMetric(IMetricManager manager)
    {
        // TODO
    }

    //--------------------------------------------------------------------------------
    // Gpu
    //--------------------------------------------------------------------------------

    private void SetupGpuMetric(IMetricManager manager)
    {
        // TODO
    }

    //--------------------------------------------------------------------------------
    // Power
    //--------------------------------------------------------------------------------

    private void SetupPowerMetric(IMetricManager manager)
    {
        // TODO
    }

    //--------------------------------------------------------------------------------
    // HardwareMonitor
    //--------------------------------------------------------------------------------

    private void SetupHardwareMonitorMetric(IMetricManager manager)
    {
        // TODO
    }
}
