namespace PrometheusExporter.Instrumentation.Mac;

using MacDotNet.SystemInfo;

using PrometheusExporter.Abstractions;

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
        if (options.HardwareMonitor.Length > 0)
        {
            SetupHardwareMonitorMetric(manager, options.HardwareMonitor, options.Fan);
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

    private static bool IsTarget(IEnumerable<string> targets, string name) =>
        targets.Any(x => (x == "*") || (x == name));

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
        var cpu = PlatformProvider.GetCpuStat();

        var efficiencyCorePrevious = InitPrevious(cpu.EfficiencyCores.Count);
        var performanceCorePrevious = InitPrevious(cpu.PerformanceCores.Count);
        var totalPrevious = new PreviousCpuTotal();
        var efficiencyPrevious = new PreviousCpuTotal();
        var performancePrevious = new PreviousCpuTotal();

        prepareEntries.Add(() =>
        {
            for (var i = 0; i < cpu.EfficiencyCores.Count; i++)
            {
                UpdatePrevious(efficiencyCorePrevious[i], cpu.EfficiencyCores[i]);
            }
            for (var i = 0; i < cpu.PerformanceCores.Count; i++)
            {
                UpdatePrevious(performanceCorePrevious[i], cpu.PerformanceCores[i]);
            }

            UpdatePreviousGroup(totalPrevious, cpu.CpuCores);
            UpdatePreviousGroup(efficiencyPrevious, cpu.EfficiencyCores);
            UpdatePreviousGroup(performancePrevious, cpu.PerformanceCores);

            cpu.Update();
        });

        var metricCpuTime = manager.CreateCounter("system_cpu_time_total");
        var metricCpuLoad = manager.CreateGauge("system_cpu_load");

        // Per efficiency core
        for (var i = 0; i < cpu.EfficiencyCores.Count; i++)
        {
            var core = cpu.EfficiencyCores[i];
            var prev = efficiencyCorePrevious[i];
            updateEntries.Add(MakeEntry(() => core.User, metricCpuTime.Create(MakeTags(new("name", $"cpu{core.Number}"), new("type", "efficiency"), new("mode", "user")))));
            updateEntries.Add(MakeEntry(() => core.System, metricCpuTime.Create(MakeTags(new("name", $"cpu{core.Number}"), new("type", "efficiency"), new("mode", "system")))));
            updateEntries.Add(MakeEntry(() => core.Idle, metricCpuTime.Create(MakeTags(new("name", $"cpu{core.Number}"), new("type", "efficiency"), new("mode", "idle")))));
            updateEntries.Add(MakeEntry(() => core.Nice, metricCpuTime.Create(MakeTags(new("name", $"cpu{core.Number}"), new("type", "efficiency"), new("mode", "nice")))));
            updateEntries.Add(MakeEntry(() => CalcCoreLoad(core, prev), metricCpuLoad.Create(MakeTags(new("name", $"cpu{core.Number}"), new("type", "efficiency")))));
        }

        // Per performance core
        for (var i = 0; i < cpu.PerformanceCores.Count; i++)
        {
            var core = cpu.PerformanceCores[i];
            var prev = performanceCorePrevious[i];
            updateEntries.Add(MakeEntry(() => core.User, metricCpuTime.Create(MakeTags(new("name", $"cpu{core.Number}"), new("type", "performance"), new("mode", "user")))));
            updateEntries.Add(MakeEntry(() => core.System, metricCpuTime.Create(MakeTags(new("name", $"cpu{core.Number}"), new("type", "performance"), new("mode", "system")))));
            updateEntries.Add(MakeEntry(() => core.Idle, metricCpuTime.Create(MakeTags(new("name", $"cpu{core.Number}"), new("type", "performance"), new("mode", "idle")))));
            updateEntries.Add(MakeEntry(() => core.Nice, metricCpuTime.Create(MakeTags(new("name", $"cpu{core.Number}"), new("type", "performance"), new("mode", "nice")))));
            updateEntries.Add(MakeEntry(() => CalcCoreLoad(core, prev), metricCpuLoad.Create(MakeTags(new("name", $"cpu{core.Number}"), new("type", "performance")))));
        }

        // Group
        updateEntries.Add(MakeEntry(() => CalcGroupLoad(cpu.CpuCores, totalPrevious), metricCpuLoad.Create(MakeTags(new("name", "cpu"), new("type", "all")))));
        if (cpu.EfficiencyCores.Count > 0)
        {
            updateEntries.Add(MakeEntry(() => CalcGroupLoad(cpu.EfficiencyCores, efficiencyPrevious), metricCpuLoad.Create(MakeTags(new("name", "cpu"), new("type", "efficiency")))));
        }
        if (cpu.PerformanceCores.Count > 0)
        {
            updateEntries.Add(MakeEntry(() => CalcGroupLoad(cpu.PerformanceCores, performancePrevious), metricCpuLoad.Create(MakeTags(new("name", "cpu"), new("type", "performance")))));
        }

        return;

        static PreviousCpuTotal[] InitPrevious(int count)
        {
            var previous = new PreviousCpuTotal[count];
            for (var i = 0; i < count; i++)
            {
                previous[i] = new PreviousCpuTotal();
            }
            return previous;
        }

        static double CalcCoreLoad(CpuCoreStat core, PreviousCpuTotal previous)
        {
            var nonIdle = (double)(core.User + core.System + core.Nice);
            var total = nonIdle + core.Idle;
            var totalDiff = total - previous.Total;
            var nonIdleDiff = nonIdle - previous.NonIdle;
            return totalDiff == 0 ? 0 : nonIdleDiff / totalDiff * 100.0;
        }

        static void UpdatePrevious(PreviousCpuTotal previous, CpuCoreStat core)
        {
            var nonIdle = (double)(core.User + core.System + core.Nice);
            var idle = (double)core.Idle;
            previous.NonIdle = nonIdle;
            previous.Total = idle + nonIdle;
        }

        static void UpdatePreviousGroup(PreviousCpuTotal previous, IReadOnlyList<CpuCoreStat> cores)
        {
            var nonIdle = 0.0;
            var idle = 0.0;
            // ReSharper disable once ForCanBeConvertedToForeach
            for (var i = 0; i < cores.Count; i++)
            {
                nonIdle += cores[i].User + cores[i].System + (double)cores[i].Nice;
                idle += cores[i].Idle;
            }
            previous.NonIdle = nonIdle;
            previous.Total = idle + nonIdle;
        }

        static double CalcGroupLoad(IReadOnlyList<CpuCoreStat> cores, PreviousCpuTotal previous)
        {
            var nonIdle = 0.0;
            var idle = 0.0;
            // ReSharper disable once ForCanBeConvertedToForeach
            for (var i = 0; i < cores.Count; i++)
            {
                nonIdle += cores[i].User + cores[i].System + (double)cores[i].Nice;
                idle += cores[i].Idle;
            }
            var total = idle + nonIdle;
            var totalDiff = total - previous.Total;
            var nonIdleDiff = nonIdle - previous.NonIdle;
            return totalDiff == 0 ? 0 : nonIdleDiff / totalDiff * 100.0;
        }
    }

    private sealed class PreviousCpuTotal
    {
        public double NonIdle { get; set; }

        public double Total { get; set; }
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
        var memory = PlatformProvider.GetMemoryStat();

        prepareEntries.Add(() => memory.Update());

        var metricMem = manager.CreateGauge("system_memory_mem");
        updateEntries.Add(MakeEntry(() => memory.PhysicalMemory, metricMem.Create(MakeTags([new("type", "total")]))));
        updateEntries.Add(MakeEntry(() => (memory.ActiveCount + memory.WireCount) * memory.PageSize, metricMem.Create(MakeTags([new("type", "used")]))));
        updateEntries.Add(MakeEntry(() => memory.FreeCount * memory.PageSize, metricMem.Create(MakeTags([new("type", "free")]))));
        updateEntries.Add(MakeEntry(() => memory.InactiveCount * memory.PageSize, metricMem.Create(MakeTags([new("type", "inactive")]))));

        var metricLoad = manager.CreateGauge("system_memory_load");
        updateEntries.Add(MakeEntry(() => CalcMemoryLoad(memory), metricLoad.Create(MakeTags())));

        var metricPage = manager.CreateCounter("system_memory_page_total");
        updateEntries.Add(MakeEntry(() => memory.PageIn, metricPage.Create(MakeTags([new("type", "in")]))));
        updateEntries.Add(MakeEntry(() => memory.PageOut, metricPage.Create(MakeTags([new("type", "out")]))));

        var metricSwapIo = manager.CreateCounter("system_memory_swap_io_total");
        updateEntries.Add(MakeEntry(() => memory.SwapIn, metricSwapIo.Create(MakeTags([new("type", "in")]))));
        updateEntries.Add(MakeEntry(() => memory.SwapOut, metricSwapIo.Create(MakeTags([new("type", "out")]))));

        return;

        static double CalcMemoryLoad(MemoryStat memory)
        {
            var used = memory.AppMemoryBytes + memory.WiredBytes;
            return memory.PhysicalMemory > 0 ? (double)used / memory.PhysicalMemory * 100.0 : 0;
        }
    }

    //--------------------------------------------------------------------------------
    // SwapUsage
    //--------------------------------------------------------------------------------

    private void SetupSwapUsageMetric(IMetricManager manager)
    {
        var swap = PlatformProvider.GetSwapUsage();

        prepareEntries.Add(() => swap.Update());

        var metric = manager.CreateGauge("system_swap");
        updateEntries.Add(MakeEntry(() => swap.TotalBytes, metric.Create(MakeTags([new("type", "total")]))));
        updateEntries.Add(MakeEntry(() => swap.UsedBytes, metric.Create(MakeTags([new("type", "used")]))));
        updateEntries.Add(MakeEntry(() => swap.AvailableBytes, metric.Create(MakeTags([new("type", "available")]))));
    }

    //--------------------------------------------------------------------------------
    // FileSystem
    //--------------------------------------------------------------------------------

    private void SetupFileSystemMetric(IMetricManager manager)
    {
        var fs = PlatformProvider.GetFileSystemStat();

        prepareEntries.Add(() => fs.Update());

        var metricSizeUsed = manager.CreateGauge("system_partition_size_used");
        var metricSizeTotal = manager.CreateGauge("system_partition_size_total");
        var metricSizeFree = manager.CreateGauge("system_partition_size_free");
        var metricSizeAvailable = manager.CreateGauge("system_partition_size_available");
        var metricFilesTotal = manager.CreateGauge("system_partition_files_total");
        var metricFilesFree = manager.CreateGauge("system_partition_files_free");

        foreach (var entry in fs.Entries)
        {
            var tags = MakeTags(new("name", entry.DeviceName), new("mount", entry.MountPoint), new("fs", entry.FileSystem));
            updateEntries.Add(MakeEntry(() => CalcFsUsage(entry), metricSizeUsed.Create(tags)));
            updateEntries.Add(MakeEntry(() => entry.TotalSize, metricSizeTotal.Create(tags)));
            updateEntries.Add(MakeEntry(() => entry.FreeSize, metricSizeFree.Create(tags)));
            updateEntries.Add(MakeEntry(() => entry.AvailableSize, metricSizeAvailable.Create(tags)));
            updateEntries.Add(MakeEntry(() => entry.TotalFiles, metricFilesTotal.Create(tags)));
            updateEntries.Add(MakeEntry(() => entry.FreeFiles, metricFilesFree.Create(tags)));
        }

        return;

        static double CalcFsUsage(FileSystemEntry e)
        {
            var used = e.TotalSize - e.FreeSize;
            var total = used + e.AvailableSize;
            return total > 0 ? (double)used / total * 100.0 : 0;
        }
    }

    //--------------------------------------------------------------------------------
    // Disk
    //--------------------------------------------------------------------------------

    private void SetupDiskMetric(IMetricManager manager)
    {
        var disk = PlatformProvider.GetDiskStat();

        prepareEntries.Add(() => disk.Update());

        var metricBytesRead = manager.CreateCounter("system_disk_bytes_total");
        var metricCompleted = manager.CreateCounter("system_disk_completed_total");
        var metricTime = manager.CreateCounter("system_disk_time_total");
        var metricErrors = manager.CreateCounter("system_disk_errors_total");

        foreach (var device in disk.Devices)
        {
            updateEntries.Add(MakeEntry(() => device.BytesRead, metricBytesRead.Create(MakeTags(new("name", device.BsdName), new("type", "read")))));
            updateEntries.Add(MakeEntry(() => device.BytesWrite, metricBytesRead.Create(MakeTags(new("name", device.BsdName), new("type", "write")))));
            updateEntries.Add(MakeEntry(() => device.ReadsCompleted, metricCompleted.Create(MakeTags(new("name", device.BsdName), new("type", "read")))));
            updateEntries.Add(MakeEntry(() => device.WritesCompleted, metricCompleted.Create(MakeTags(new("name", device.BsdName), new("type", "write")))));
            updateEntries.Add(MakeEntry(() => device.TotalTimeRead, metricTime.Create(MakeTags(new("name", device.BsdName), new("type", "read")))));
            updateEntries.Add(MakeEntry(() => device.TotalTimeWrite, metricTime.Create(MakeTags(new("name", device.BsdName), new("type", "write")))));
            updateEntries.Add(MakeEntry(() => device.ErrorsRead, metricErrors.Create(MakeTags(new("name", device.BsdName), new("type", "read")))));
            updateEntries.Add(MakeEntry(() => device.ErrorsWrite, metricErrors.Create(MakeTags(new("name", device.BsdName), new("type", "write")))));
        }
    }

    //--------------------------------------------------------------------------------
    // FileDescriptor
    //--------------------------------------------------------------------------------

    private void SetupFileDescriptorMetric(IMetricManager manager)
    {
        var fd = PlatformProvider.GetFileHandleStat();

        prepareEntries.Add(() => fd.Update());

        var metricOpenFiles = manager.CreateGauge("system_fd_open_files");
        updateEntries.Add(MakeEntry(() => fd.OpenFiles, metricOpenFiles.Create(MakeTags())));

        var metricOpenVnodes = manager.CreateGauge("system_fd_open_vnodes");
        updateEntries.Add(MakeEntry(() => fd.OpenVnodes, metricOpenVnodes.Create(MakeTags())));
    }

    //--------------------------------------------------------------------------------
    // Network
    //--------------------------------------------------------------------------------

    private void SetupNetworkMetric(IMetricManager manager)
    {
        var network = PlatformProvider.GetNetworkStat();

        prepareEntries.Add(() => network.Update());

        var metricBytes = manager.CreateCounter("system_network_bytes_total");
        var metricPackets = manager.CreateCounter("system_network_packets_total");
        var metricErrors = manager.CreateCounter("system_network_errors_total");
        var metricDropped = manager.CreateCounter("system_network_dropped_total");
        var metricMulticast = manager.CreateCounter("system_network_multicast_total");
        var metricCollisions = manager.CreateCounter("system_network_collisions_total");

        foreach (var nif in network.Interfaces)
        {
            updateEntries.Add(MakeEntry(() => nif.RxBytes, metricBytes.Create(MakeTags(new("name", nif.Name), new("type", "rx")))));
            updateEntries.Add(MakeEntry(() => nif.TxBytes, metricBytes.Create(MakeTags(new("name", nif.Name), new("type", "tx")))));
            updateEntries.Add(MakeEntry(() => nif.RxPackets, metricPackets.Create(MakeTags(new("name", nif.Name), new("type", "rx")))));
            updateEntries.Add(MakeEntry(() => nif.TxPackets, metricPackets.Create(MakeTags(new("name", nif.Name), new("type", "tx")))));
            updateEntries.Add(MakeEntry(() => nif.RxErrors, metricErrors.Create(MakeTags(new("name", nif.Name), new("type", "rx")))));
            updateEntries.Add(MakeEntry(() => nif.TxErrors, metricErrors.Create(MakeTags(new("name", nif.Name), new("type", "tx")))));
            updateEntries.Add(MakeEntry(() => nif.RxDrops, metricDropped.Create(MakeTags(new("name", nif.Name), new("type", "rx")))));
            updateEntries.Add(MakeEntry(() => nif.RxMulticast, metricMulticast.Create(MakeTags(new("name", nif.Name), new("type", "rx")))));
            updateEntries.Add(MakeEntry(() => nif.TxMulticast, metricMulticast.Create(MakeTags(new("name", nif.Name), new("type", "tx")))));
            updateEntries.Add(MakeEntry(() => nif.Collisions, metricCollisions.Create(MakeTags(new("name", nif.Name), new("type", "tx")))));
        }
    }

    //--------------------------------------------------------------------------------
    // ProcessSummary
    //--------------------------------------------------------------------------------

    private void SetupProcessSummaryMetric(IMetricManager manager)
    {
        var process = PlatformProvider.GetProcessSummary();

        prepareEntries.Add(() => process.Update());

        var metricProcess = manager.CreateGauge("system_process_count");
        updateEntries.Add(MakeEntry(() => process.ProcessCount, metricProcess.Create(MakeTags())));

        var metricThread = manager.CreateGauge("system_thread_count");
        updateEntries.Add(MakeEntry(() => process.ThreadCount, metricThread.Create(MakeTags())));
    }

    //--------------------------------------------------------------------------------
    // CpuFrequency
    //--------------------------------------------------------------------------------

    private void SetupCpuFrequencyMetric(IMetricManager manager)
    {
        var cpuFreq = PlatformProvider.GetCpuFrequency();

        prepareEntries.Add(() => cpuFreq.Update());

        var metric = manager.CreateGauge("hardware_cpu_frequency");

        // Per core
        foreach (var core in cpuFreq.Cores)
        {
            var coreType = core.CoreType == CpuCoreType.Efficiency ? "efficiency" : "performance";
            updateEntries.Add(MakeEntry(() => core.Frequency, metric.Create(MakeTags(new("name", $"cpu{core.Number}"), new("type", coreType)))));
        }

        // Group
        updateEntries.Add(MakeEntry(() => CalcAvgFrequency(cpuFreq.Cores), metric.Create(MakeTags(new("name", "cpu"), new("type", "all")))));
        if (cpuFreq.EfficiencyCores.Count > 0)
        {
            updateEntries.Add(MakeEntry(() => CalcAvgFrequency(cpuFreq.EfficiencyCores), metric.Create(MakeTags(new("name", "cpu"), new("type", "efficiency")))));
        }
        if (cpuFreq.PerformanceCores.Count > 0)
        {
            updateEntries.Add(MakeEntry(() => CalcAvgFrequency(cpuFreq.PerformanceCores), metric.Create(MakeTags(new("name", "cpu"), new("type", "performance")))));
        }

        return;

        static double CalcAvgFrequency(IReadOnlyList<CpuCoreFrequency> cores)
        {
            var sum = 0.0;
            // ReSharper disable once LoopCanBeConvertedToQuery
            // ReSharper disable once ForCanBeConvertedToForeach
            for (var i = 0; i < cores.Count; i++)
            {
                sum += cores[i].Frequency;
            }
            return sum / cores.Count;
        }
    }

    //--------------------------------------------------------------------------------
    // Gpu
    //--------------------------------------------------------------------------------

    private void SetupGpuMetric(IMetricManager manager)
    {
        var gpus = PlatformProvider.GetGpuDevices();

        prepareEntries.Add(() =>
        {
            foreach (var gpu in gpus)
            {
                gpu.Update();
            }
        });

        var metricUtilization = manager.CreateGauge("hardware_gpu_utilization");
        var metricMemory = manager.CreateGauge("hardware_gpu_memory");

        foreach (var gpu in gpus)
        {
            updateEntries.Add(MakeEntry(() => gpu.DeviceUtilization, metricUtilization.Create(MakeTags(new("name", gpu.Name), new("type", "device")))));
            updateEntries.Add(MakeEntry(() => gpu.RendererUtilization, metricUtilization.Create(MakeTags(new("name", gpu.Name), new("type", "renderer")))));
            updateEntries.Add(MakeEntry(() => gpu.TilerUtilization, metricUtilization.Create(MakeTags(new("name", gpu.Name), new("type", "tiler")))));
            updateEntries.Add(MakeEntry(() => gpu.AllocSystemMemory, metricMemory.Create(MakeTags(new("name", gpu.Name), new("type", "alloc")))));
            updateEntries.Add(MakeEntry(() => gpu.InUseSystemMemory, metricMemory.Create(MakeTags(new("name", gpu.Name), new("type", "in_use")))));
        }
    }

    //--------------------------------------------------------------------------------
    // Power
    //--------------------------------------------------------------------------------

    private void SetupPowerMetric(IMetricManager manager)
    {
        var power = PlatformProvider.GetPowerStat();
        if (!power.Supported)
        {
            return;
        }

        prepareEntries.Add(() => power.Update());

        var metric = manager.CreateGauge("hardware_power_energy");
        updateEntries.Add(MakeEntry(() => power.Cpu, metric.Create(MakeTags([new("type", "cpu")]))));
        updateEntries.Add(MakeEntry(() => power.Gpu, metric.Create(MakeTags([new("type", "gpu")]))));
        updateEntries.Add(MakeEntry(() => power.Ane, metric.Create(MakeTags([new("type", "ane")]))));
        updateEntries.Add(MakeEntry(() => power.Ram, metric.Create(MakeTags([new("type", "ram")]))));
        updateEntries.Add(MakeEntry(() => power.Pci, metric.Create(MakeTags([new("type", "pci")]))));
        updateEntries.Add(MakeEntry(() => power.Total, metric.Create(MakeTags([new("type", "total")]))));
    }

    //--------------------------------------------------------------------------------
    // HardwareMonitor
    //--------------------------------------------------------------------------------

    private void SetupHardwareMonitorMetric(IMetricManager manager, string[] targets, bool fan)
    {
        var smc = PlatformProvider.GetSmcMonitor();

        prepareEntries.Add(() => smc.Update());

        var metric = manager.CreateGauge("hardware_monitor");
        foreach (var sensor in smc.Temperatures.Where(s => IsTarget(targets, s.Key)))
        {
            updateEntries.Add(MakeEntry(() => sensor.Value, metric.Create(MakeTags(new("type", "temperature"), new("key", sensor.Key), new("description", sensor.Description)))));
        }
        foreach (var sensor in smc.Voltages.Where(s => IsTarget(targets, s.Key)))
        {
            updateEntries.Add(MakeEntry(() => sensor.Value, metric.Create(MakeTags(new("type", "voltage"), new("key", sensor.Key), new("description", sensor.Description)))));
        }
        foreach (var sensor in smc.Currents.Where(s => IsTarget(targets, s.Key)))
        {
            updateEntries.Add(MakeEntry(() => sensor.Value, metric.Create(MakeTags(new("type", "current"), new("key", sensor.Key), new("description", sensor.Description)))));
        }
        foreach (var sensor in smc.Powers.Where(s => IsTarget(targets, s.Key)))
        {
            updateEntries.Add(MakeEntry(() => sensor.Value, metric.Create(MakeTags(new("type", "power"), new("key", sensor.Key), new("description", sensor.Description)))));
        }

        if (fan && smc.Fans.Count > 0)
        {
            var metricFan = manager.CreateGauge("hardware_monitor_fan_rpm");
            foreach (var f in smc.Fans)
            {
                updateEntries.Add(MakeEntry(() => f.ActualRpm, metricFan.Create(MakeTags([new("index", f.Index)]))));
            }
        }
    }
}
