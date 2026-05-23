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
        var cpu = PlatformProvider.GetCpuStat();

        var corePrevious = Array.Empty<PreviousCpuTotal>();

        prepareEntries.Add(() =>
        {
            if (corePrevious.Length < cpu.CpuCores.Count)
            {
                var newPrevious = new PreviousCpuTotal[cpu.CpuCores.Count];
                for (var i = 0; i < newPrevious.Length; i++)
                {
                    newPrevious[i] = corePrevious.Length > i ? corePrevious[i] : new PreviousCpuTotal();
                }
                corePrevious = newPrevious;
            }

            for (var i = 0; i < corePrevious.Length && i < cpu.CpuCores.Count; i++)
            {
                UpdatePrevious(corePrevious[i], cpu.CpuCores[i]);
            }

            cpu.Update();
        });

        var metricCpuTime = manager.CreateCounter("system_cpu_time_total");
        var metricCpuLoad = manager.CreateGauge("system_cpu_load");

        for (var i = 0; i < cpu.CpuCores.Count; i++)
        {
            var core = cpu.CpuCores[i];
            updateEntries.Add(MakeEntry(() => core.User, metricCpuTime.Create(MakeTags(new("name", $"cpu{core.Number}"), new("mode", "user")))));
            updateEntries.Add(MakeEntry(() => core.System, metricCpuTime.Create(MakeTags(new("name", $"cpu{core.Number}"), new("mode", "system")))));
            updateEntries.Add(MakeEntry(() => core.Idle, metricCpuTime.Create(MakeTags(new("name", $"cpu{core.Number}"), new("mode", "idle")))));
            updateEntries.Add(MakeEntry(() => core.Nice, metricCpuTime.Create(MakeTags(new("name", $"cpu{core.Number}"), new("mode", "nice")))));

            var coreCapture = core;
            var prevIndex = i;
            updateEntries.Add(MakeEntry(() =>
            {
                if (prevIndex >= corePrevious.Length)
                {
                    return 0;
                }

                var prev = corePrevious[prevIndex];
                var nonIdle = (double)(coreCapture.User + coreCapture.System + coreCapture.Nice);
                var total = nonIdle + coreCapture.Idle;
                var totalDiff = total - prev.Total;
                var nonIdleDiff = nonIdle - prev.NonIdle;
                return totalDiff == 0 ? 0 : nonIdleDiff / totalDiff * 100.0;
            }, metricCpuLoad.Create(MakeTags([new("name", $"cpu{core.Number}")]))));
        }

        return;

        static void UpdatePrevious(PreviousCpuTotal previous, CpuCoreStat core)
        {
            var nonIdle = (double)(core.User + core.System + core.Nice);
            var idle = (double)core.Idle;
            previous.NonIdle = nonIdle;
            previous.Total = idle + nonIdle;
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
        updateEntries.Add(MakeEntry(() =>
        {
            var used = memory.AppMemoryBytes + memory.WiredBytes;
            return memory.PhysicalMemory > 0 ? (double)used / memory.PhysicalMemory * 100.0 : 0;
        }, metricLoad.Create(MakeTags())));

        var metricPage = manager.CreateCounter("system_memory_page_total");
        updateEntries.Add(MakeEntry(() => memory.PageIn, metricPage.Create(MakeTags([new("type", "in")]))));
        updateEntries.Add(MakeEntry(() => memory.PageOut, metricPage.Create(MakeTags([new("type", "out")]))));

        var metricSwapIo = manager.CreateCounter("system_memory_swap_io_total");
        updateEntries.Add(MakeEntry(() => memory.SwapIn, metricSwapIo.Create(MakeTags([new("type", "in")]))));
        updateEntries.Add(MakeEntry(() => memory.SwapOut, metricSwapIo.Create(MakeTags([new("type", "out")]))));
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

        updateEntries.Add(() =>
        {
            foreach (var entry in fs.Entries)
            {
                var tags = MakeTags(new("name", entry.DeviceName), new("mount", entry.MountPoint), new("fs", entry.FileSystem));
                var used = entry.TotalSize - entry.FreeSize;
                var total = used + entry.AvailableSize;
                metricSizeUsed.Create(tags).Value = total > 0 ? (double)used / total * 100.0 : 0;
                metricSizeTotal.Create(tags).Value = entry.TotalSize;
                metricSizeFree.Create(tags).Value = entry.FreeSize;
                metricSizeAvailable.Create(tags).Value = entry.AvailableSize;
                metricFilesTotal.Create(tags).Value = entry.TotalFiles;
                metricFilesFree.Create(tags).Value = entry.FreeFiles;
            }
        });
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

        updateEntries.Add(() =>
        {
            foreach (var device in disk.Devices)
            {
                metricBytesRead.Create(MakeTags(new("name", device.BsdName), new("type", "read"))).Value = device.BytesRead;
                metricBytesRead.Create(MakeTags(new("name", device.BsdName), new("type", "write"))).Value = device.BytesWrite;
                metricCompleted.Create(MakeTags(new("name", device.BsdName), new("type", "read"))).Value = device.ReadsCompleted;
                metricCompleted.Create(MakeTags(new("name", device.BsdName), new("type", "write"))).Value = device.WritesCompleted;
                metricTime.Create(MakeTags(new("name", device.BsdName), new("type", "read"))).Value = device.TotalTimeRead;
                metricTime.Create(MakeTags(new("name", device.BsdName), new("type", "write"))).Value = device.TotalTimeWrite;
                metricErrors.Create(MakeTags(new("name", device.BsdName), new("type", "read"))).Value = device.ErrorsRead;
                metricErrors.Create(MakeTags(new("name", device.BsdName), new("type", "write"))).Value = device.ErrorsWrite;
            }
        });
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

        updateEntries.Add(() =>
        {
            foreach (var nif in network.Interfaces)
            {
                metricBytes.Create(MakeTags(new("name", nif.Name), new("type", "rx"))).Value = nif.RxBytes;
                metricBytes.Create(MakeTags(new("name", nif.Name), new("type", "tx"))).Value = nif.TxBytes;
                metricPackets.Create(MakeTags(new("name", nif.Name), new("type", "rx"))).Value = nif.RxPackets;
                metricPackets.Create(MakeTags(new("name", nif.Name), new("type", "tx"))).Value = nif.TxPackets;
                metricErrors.Create(MakeTags(new("name", nif.Name), new("type", "rx"))).Value = nif.RxErrors;
                metricErrors.Create(MakeTags(new("name", nif.Name), new("type", "tx"))).Value = nif.TxErrors;
                metricDropped.Create(MakeTags(new("name", nif.Name), new("type", "rx"))).Value = nif.RxDrops;
                metricMulticast.Create(MakeTags(new("name", nif.Name), new("type", "rx"))).Value = nif.RxMulticast;
                metricMulticast.Create(MakeTags(new("name", nif.Name), new("type", "tx"))).Value = nif.TxMulticast;
                metricCollisions.Create(MakeTags(new("name", nif.Name), new("type", "tx"))).Value = nif.Collisions;
            }
        });
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

        updateEntries.Add(() =>
        {
            foreach (var core in cpuFreq.Cores)
            {
                var coreType = core.CoreType == CpuCoreType.Efficiency ? "efficiency" : "performance";
                metric.Create(MakeTags(new("name", $"cpu{core.Number}"), new("type", coreType))).Value = core.Frequency;
            }
        });
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

    private void SetupHardwareMonitorMetric(IMetricManager manager)
    {
        var smc = PlatformProvider.GetSmcMonitor();

        prepareEntries.Add(() => smc.Update());

        if (smc.Temperatures.Count > 0)
        {
            var metric = manager.CreateGauge("hardware_monitor_temperature");
            foreach (var sensor in smc.Temperatures)
            {
                updateEntries.Add(MakeEntry(() => sensor.Value, metric.Create(MakeTags(new("key", sensor.Key), new("description", sensor.Description)))));
            }
        }

        if (smc.Voltages.Count > 0)
        {
            var metric = manager.CreateGauge("hardware_monitor_voltage");
            foreach (var sensor in smc.Voltages)
            {
                updateEntries.Add(MakeEntry(() => sensor.Value, metric.Create(MakeTags(new("key", sensor.Key), new("description", sensor.Description)))));
            }
        }

        if (smc.Currents.Count > 0)
        {
            var metric = manager.CreateGauge("hardware_monitor_current");
            foreach (var sensor in smc.Currents)
            {
                updateEntries.Add(MakeEntry(() => sensor.Value, metric.Create(MakeTags(new("key", sensor.Key), new("description", sensor.Description)))));
            }
        }

        if (smc.Powers.Count > 0)
        {
            var metric = manager.CreateGauge("hardware_monitor_power");
            foreach (var sensor in smc.Powers)
            {
                updateEntries.Add(MakeEntry(() => sensor.Value, metric.Create(MakeTags(new("key", sensor.Key), new("description", sensor.Description)))));
            }
        }

        if (smc.Fans.Count > 0)
        {
            var metricActual = manager.CreateGauge("hardware_monitor_fan_rpm");
            foreach (var fan in smc.Fans)
            {
                updateEntries.Add(MakeEntry(() => fan.ActualRpm, metricActual.Create(MakeTags([new("index", fan.Index)]))));
            }
        }
    }
}
