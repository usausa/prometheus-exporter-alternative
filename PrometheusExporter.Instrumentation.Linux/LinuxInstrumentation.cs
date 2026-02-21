namespace PrometheusExporter.Instrumentation.Linux;

using LinuxDotNet.SystemInfo;

using PrometheusExporter.Abstractions;

internal sealed class LinuxInstrumentation
{
    private readonly string host;

    private readonly TimeSpan updateDuration;

    private readonly List<Action> prepareEntries = [];

    private readonly List<Action> updateEntries = [];

    private DateTime lastUpdate;

    public LinuxInstrumentation(
        LinuxOptions options,
        IInstrumentationEnvironment environment,
        IMetricManager manager)
    {
        host = environment.Host;
        updateDuration = TimeSpan.FromMilliseconds(options.UpdateDuration);

        if (options.Uptime)
        {
            SetupUptimeMetric(manager);
        }
        if (options.SystemStat)
        {
            SetupSystemStatMetric(manager);
        }
        if (options.LoadAverage)
        {
            SetupLoadAverageMetric(manager);
        }
        if (options.Memory.Length > 0)
        {
            SetupMemoryMetric(manager, options.Memory);
        }
        if (options.VirtualMemory.Length > 0)
        {
            SetupVirtualMemoryMetric(manager, options.VirtualMemory);
        }
        if (options.Mount)
        {
            SetupMountMetric(manager);
        }
        if (options.DiskStat)
        {
            SetupDiskStatMetric(manager);
        }
        if (options.FileDescriptor)
        {
            SetupFileDescriptorMetric(manager);
        }
        if (options.NetworkStat)
        {
            SetupNetworkStatMetric(manager);
        }
        if (options.TcpStat || options.Tcp6Stat)
        {
            SetupTcpStaticMetric(manager, options.TcpStat, options.Tcp6Stat);
        }
        if (options.WirelessStat)
        {
            SetupWirelessStatMetric(manager);
        }
        if (options.ProcessSummary)
        {
            SetupProcessSummaryMetric(manager);
        }
        if (options.Cpu)
        {
            SetupCpuMetric(manager);
        }
        if (options.Battery)
        {
            SetupBatteryMetric(manager);
        }
        if (options.Mains)
        {
            SetupMainsMetric(manager);
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
    // System
    //--------------------------------------------------------------------------------

    private void SetupSystemStatMetric(IMetricManager manager)
    {
        var stat = PlatformProvider.GetSystemStat();

        var totalPrevious = new PreviousCpuTotal();
        var corePrevious = new PreviousCpuTotal[stat.CpuCores.Count];
        for (var i = 0; i < corePrevious.Length; i++)
        {
            corePrevious[i] = new PreviousCpuTotal();
        }

        prepareEntries.Add(() =>
        {
            UpdatePrevious(totalPrevious, stat.CpuTotal);
            for (var i = 0; i < corePrevious.Length; i++)
            {
                UpdatePrevious(corePrevious[i], stat.CpuCores[i]);
            }

            stat.Update();
        });

        var metricInterrupt = manager.CreateCounter("system_interrupt_total");
        var metricContextSwitch = manager.CreateCounter("system_context_switch_total");
        var metricForks = manager.CreateCounter("system_forks_total");
        var metricScheduler = manager.CreateGauge("system_scheduler_task");
        var metricSoftIrq = manager.CreateCounter("system_softirq_total");

        updateEntries.Add(MakeEntry(() => stat.Interrupt, metricInterrupt.Create(MakeTags())));
        updateEntries.Add(MakeEntry(() => stat.ContextSwitch, metricContextSwitch.Create(MakeTags())));
        updateEntries.Add(MakeEntry(() => stat.Forks, metricForks.Create(MakeTags())));
        updateEntries.Add(MakeEntry(() => stat.RunnableTasks, metricScheduler.Create(MakeTags([new("state", "runnable")]))));
        updateEntries.Add(MakeEntry(() => stat.BlockedTasks, metricScheduler.Create(MakeTags([new("state", "blocked")]))));
        updateEntries.Add(MakeEntry(() => stat.SoftIrq, metricSoftIrq.Create(MakeTags())));

        var metricCpuTimeTotal = manager.CreateCounter("system_cpu_time_total");

        foreach (var cpu in stat.CpuCores)
        {
            SetupCpuTimeEntry(cpu);
        }
        SetupCpuTimeEntry(stat.CpuTotal);

        var metricCpuLoad = manager.CreateGauge("system_cpu_load");

        for (var i = 0; i < corePrevious.Length; i++)
        {
            SetupCpuLoadEntry(corePrevious[i], stat.CpuCores[i]);
        }
        SetupCpuLoadEntry(totalPrevious, stat.CpuTotal);

        return;

        static void UpdatePrevious(PreviousCpuTotal previous, CpuStat cpu)
        {
            var nonIdle = CalcCpuNonIdle(cpu);
            var idle = CalcCpuIdle(cpu);
            previous.NonIdle = nonIdle;
            previous.Total = idle + nonIdle;
        }

        void SetupCpuTimeEntry(CpuStat cpu)
        {
            // ReSharper disable StringLiteralTypo
            updateEntries.Add(MakeEntry(() => cpu.User, metricCpuTimeTotal.Create(MakeTags(new("name", cpu.Name), new("mode", "user")))));
            updateEntries.Add(MakeEntry(() => cpu.Nice, metricCpuTimeTotal.Create(MakeTags(new("name", cpu.Name), new("mode", "nice")))));
            updateEntries.Add(MakeEntry(() => cpu.System, metricCpuTimeTotal.Create(MakeTags(new("name", cpu.Name), new("mode", "system")))));
            updateEntries.Add(MakeEntry(() => cpu.Idle, metricCpuTimeTotal.Create(MakeTags(new("name", cpu.Name), new("mode", "idle")))));
            updateEntries.Add(MakeEntry(() => cpu.IoWait, metricCpuTimeTotal.Create(MakeTags(new("name", cpu.Name), new("mode", "iowait")))));
            updateEntries.Add(MakeEntry(() => cpu.Irq, metricCpuTimeTotal.Create(MakeTags(new("name", cpu.Name), new("mode", "irq")))));
            updateEntries.Add(MakeEntry(() => cpu.SoftIrq, metricCpuTimeTotal.Create(MakeTags(new("name", cpu.Name), new("mode", "softirq")))));
            updateEntries.Add(MakeEntry(() => cpu.Steal, metricCpuTimeTotal.Create(MakeTags(new("name", cpu.Name), new("mode", "steal")))));
            updateEntries.Add(MakeEntry(() => cpu.Guest, metricCpuTimeTotal.Create(MakeTags(new("name", cpu.Name), new("mode", "guest")))));
            updateEntries.Add(MakeEntry(() => cpu.GuestNice, metricCpuTimeTotal.Create(MakeTags(new("name", cpu.Name), new("mode", "guestnice")))));
            // ReSharper restore StringLiteralTypo
        }

        void SetupCpuLoadEntry(PreviousCpuTotal previous, CpuStat cpu)
        {
            updateEntries.Add(MakeEntry(() =>
            {
                var nonIdle = CalcCpuNonIdle(cpu);
                var idle = CalcCpuIdle(cpu);
                var total = idle + nonIdle;

                var totalDiff = total - previous.Total;
                var nonIdleDiff = nonIdle - previous.NonIdle;
                return totalDiff == 0 ? 0 : (double)nonIdleDiff / totalDiff * 100.0;
            }, metricCpuLoad.Create(MakeTags([new("name", cpu.Name)]))));
        }

        static long CalcCpuIdle(CpuStat cpu)
        {
            return cpu.Idle + cpu.IoWait;
        }

        static long CalcCpuNonIdle(CpuStat cpu)
        {
            return cpu.User + cpu.Nice + cpu.System + cpu.Irq + cpu.SoftIrq + cpu.Steal;
        }
    }

    private sealed class PreviousCpuTotal
    {
        public long NonIdle { get; set; }

        public long Total { get; set; }
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

    private void SetupMemoryMetric(IMetricManager manager, string[] targets)
    {
        var memory = PlatformProvider.GetMemoryStat();

        prepareEntries.Add(() => memory.Update());

        // ReSharper disable StringLiteralTypo
        SetupCustomMetrics("load", metric =>
        [
            MakeEntry(() => (double)(memory.MemoryTotal - memory.MemoryAvailable) / memory.MemoryTotal * 100, metric.Create(MakeTags()))
        ]);
        SetupCustomMetrics("mem", metric =>
        [
            MakeEntry(() => memory.MemoryTotal, metric.Create(MakeTags([new("type", "total")]))),
            MakeEntry(() => memory.MemoryAvailable, metric.Create(MakeTags([new("type", "available")]))),
            MakeEntry(() => memory.MemoryFree, metric.Create(MakeTags([new("type", "free")])))
        ]);
        SetupSimpleMetrics("buffers", () => memory.Buffers);
        SetupSimpleMetrics("cached", () => memory.Cached);
        SetupSimpleMetrics("swap_cached", () => memory.SwapCached);
        SetupCustomMetrics("lru", metric =>
        [
            MakeEntry(() => memory.ActiveAnonymous, metric.Create(MakeTags(new("type", "anon"), new("state", "active")))),
            MakeEntry(() => memory.InactiveAnonymous, metric.Create(MakeTags(new("type", "anon"), new("state", "inactive")))),
            MakeEntry(() => memory.ActiveFile, metric.Create(MakeTags(new("type", "file"), new("state", "active")))),
            MakeEntry(() => memory.InactiveFile, metric.Create(MakeTags(new("type", "file"), new("state", "inactive"))))
        ]);
        SetupSimpleMetrics("unevictable", () => memory.Unevictable);
        SetupSimpleMetrics("mlocked", () => memory.MemoryLocked);
        SetupCustomMetrics("swap", metric =>
        [
            MakeEntry(() => memory.SwapTotal, metric.Create(MakeTags([new("type", "total")]))),
            MakeEntry(() => memory.SwapFree, metric.Create(MakeTags([new("type", "free")])))
        ]);
        SetupSimpleMetrics("dirty", () => memory.Dirty);
        SetupSimpleMetrics("writeback", () => memory.Writeback);
        SetupSimpleMetrics("anon_pages", () => memory.AnonymousPages);
        SetupSimpleMetrics("mapped", () => memory.Mapped);
        SetupSimpleMetrics("shmem", () => memory.SharedMemory);
        SetupSimpleMetrics("k_reclaimable", () => memory.KernelReclaimable);
        SetupCustomMetrics("slab", metric =>
        [
            MakeEntry(() => memory.SlabTotal, metric.Create(MakeTags([new("type", "total")]))),
            MakeEntry(() => memory.SlabReclaimable, metric.Create(MakeTags([new("type", "reclaimable")]))),
            MakeEntry(() => memory.SlabUnreclaimable, metric.Create(MakeTags([new("type", "unreclaimable")])))
        ]);
        SetupSimpleMetrics("kernel_stack", () => memory.KernelStack);
        SetupSimpleMetrics("page_tables", () => memory.PageTables);
        SetupSimpleMetrics("commit_limit", () => memory.CommitLimit);
        SetupSimpleMetrics("committed_as", () => memory.CommittedAddressSpace);
        SetupSimpleMetrics("hardware_corrupted", () => memory.HardwareCorrupted);
        // ReSharper restore StringLiteralTypo

        void SetupSimpleMetrics(string name, Func<double> selector)
        {
            if (IsTarget(targets, name))
            {
                var metric = manager.CreateGauge($"system_memory_{name}");
                updateEntries.Add(MakeEntry(selector, metric.Create(MakeTags())));
            }
        }

        void SetupCustomMetrics(string name, Func<IMetric, Action[]> func)
        {
            if (IsTarget(targets, name))
            {
                var metric = manager.CreateGauge($"system_memory_{name}");
                updateEntries.AddRange(func(metric));
            }
        }
    }

    //--------------------------------------------------------------------------------
    // VirtualMemory
    //--------------------------------------------------------------------------------

    private void SetupVirtualMemoryMetric(IMetricManager manager, string[] targets)
    {
        var vm = PlatformProvider.GetVirtualMemoryStat();

        prepareEntries.Add(() => vm.Update());

        SetupCustomMetrics("page", metric =>
        [
            MakeEntry(() => vm.PageIn, metric.Create(MakeTags([new("type", "in")]))),
            MakeEntry(() => vm.PageOut, metric.Create(MakeTags([new("type", "out")])))
        ]);
        SetupCustomMetrics("swap", metric =>
        [
            MakeEntry(() => vm.SwapIn, metric.Create(MakeTags([new("type", "in")]))),
            MakeEntry(() => vm.SwapOut, metric.Create(MakeTags([new("type", "out")])))
        ]);
        SetupCustomMetrics("page_faults", metric =>
        [
            MakeEntry(() => vm.PageFaults, metric.Create(MakeTags([new("type", "in")]))),
            MakeEntry(() => vm.MajorPageFaults, metric.Create(MakeTags([new("type", "out")])))
        ]);
        SetupCustomMetrics("steal", metric =>
        [
            MakeEntry(() => vm.StealKernel, metric.Create(MakeTags([new("type", "kernel")]))),
            MakeEntry(() => vm.StealDirect, metric.Create(MakeTags([new("type", "direct")])))
        ]);
        SetupCustomMetrics("scan", metric =>
        [
            MakeEntry(() => vm.ScanKernel, metric.Create(MakeTags([new("type", "kernel")]))),
            MakeEntry(() => vm.ScanDirect, metric.Create(MakeTags([new("type", "direct")])))
        ]);
        SetupSimpleMetrics("oom_kill", () => vm.OutOfMemoryKiller);

        void SetupSimpleMetrics(string name, Func<double> selector)
        {
            if (IsTarget(targets, name))
            {
                var metric = manager.CreateCounter($"system_virtual_{name}_total");
                updateEntries.Add(MakeEntry(selector, metric.Create(MakeTags())));
            }
        }

        void SetupCustomMetrics(string name, Func<IMetric, Action[]> func)
        {
            if (IsTarget(targets, name))
            {
                var metric = manager.CreateCounter($"system_virtual_{name}_total");
                updateEntries.AddRange(func(metric));
            }
        }
    }

    //--------------------------------------------------------------------------------
    // Mount
    //--------------------------------------------------------------------------------

    private void SetupMountMetric(IMetricManager manager)
    {
        var partitions = PlatformProvider.GetPartitions();
        var mounts = partitions
            .SelectMany(static x => x.GetMounts())
            .Select(x => new
            {
                Mount = x,
                Usage = PlatformProvider.GetFileSystemUsage(x.MountPoint),
                Tags = MakeTags(new("name", x.DeviceName), new("mount", x.MountPoint), new("fs", x.FileSystem))
            })
            .ToArray();

        prepareEntries.Add(() =>
        {
            foreach (var item in mounts)
            {
                item.Usage.Update();
            }
        });

        var metricSizeUsed = manager.CreateGauge("system_partition_size_used");
        foreach (var item in mounts)
        {
            updateEntries.Add(MakeEntry(() => CalcUsed(item.Usage), metricSizeUsed.Create(item.Tags)));
        }

        var metricSizeTotal = manager.CreateGauge("system_partition_size_total");
        foreach (var item in mounts)
        {
            updateEntries.Add(MakeEntry(() => item.Usage.TotalSize, metricSizeTotal.Create(item.Tags)));
        }

        var metricSizeFree = manager.CreateGauge("system_partition_size_free");
        foreach (var item in mounts)
        {
            updateEntries.Add(MakeEntry(() => item.Usage.FreeSize, metricSizeFree.Create(item.Tags)));
        }

        var metricAvailableFree = manager.CreateGauge("system_partition_size_available");
        foreach (var item in mounts)
        {
            updateEntries.Add(MakeEntry(() => item.Usage.AvailableSize, metricAvailableFree.Create(item.Tags)));
        }

        var metricFilesTotal = manager.CreateGauge("system_partition_files_total");
        foreach (var item in mounts)
        {
            updateEntries.Add(MakeEntry(() => item.Usage.TotalFiles, metricFilesTotal.Create(item.Tags)));
        }

        var metricFilesFree = manager.CreateGauge("system_partition_files_free");
        foreach (var item in mounts)
        {
            updateEntries.Add(MakeEntry(() => item.Usage.FreeFiles, metricFilesFree.Create(item.Tags)));
        }

        return;

        static double CalcUsed(FileSystemUsage usage)
        {
            var used = usage.TotalSize - usage.FreeSize;
            var total = used + usage.AvailableSize;
            return total > 0
                ? (double)used / total * 100
                : 0;
        }
    }

    //--------------------------------------------------------------------------------
    // DiskStatics
    //--------------------------------------------------------------------------------

    private void SetupDiskStatMetric(IMetricManager manager)
    {
        var disk = PlatformProvider.GetDiskStat();

        prepareEntries.Add(() => disk.Update());

        var metricCompleted = manager.CreateCounter("system_disk_completed_total");
        var metricMerged = manager.CreateCounter("system_disk_merged_total");
        var metricSectors = manager.CreateCounter("system_disk_sectors_total");
        var metricTime = manager.CreateCounter("system_disk_time_total");
        var metricIosInProgress = manager.CreateGauge("system_disk_ios_in_progress");
        var metricIoTime = manager.CreateCounter("system_disk_io_time_total");
        var metricWeightIoTime = manager.CreateCounter("system_disk_weight_io_time_total");

        foreach (var device in disk.Devices)
        {
            updateEntries.Add(MakeEntry(() => device.ReadCompleted, metricCompleted.Create(MakeTags(new("name", device.Name), new("type", "read")))));
            updateEntries.Add(MakeEntry(() => device.ReadMerged, metricMerged.Create(MakeTags(new("name", device.Name), new("type", "read")))));
            updateEntries.Add(MakeEntry(() => device.ReadSectors, metricSectors.Create(MakeTags(new("name", device.Name), new("type", "read")))));
            updateEntries.Add(MakeEntry(() => device.ReadTime, metricTime.Create(MakeTags(new("name", device.Name), new("type", "read")))));

            updateEntries.Add(MakeEntry(() => device.WriteCompleted, metricCompleted.Create(MakeTags(new("name", device.Name), new("type", "write")))));
            updateEntries.Add(MakeEntry(() => device.WriteMerged, metricMerged.Create(MakeTags(new("name", device.Name), new("type", "write")))));
            updateEntries.Add(MakeEntry(() => device.WriteSectors, metricSectors.Create(MakeTags(new("name", device.Name), new("type", "write")))));
            updateEntries.Add(MakeEntry(() => device.WriteTime, metricTime.Create(MakeTags(new("name", device.Name), new("type", "write")))));

            updateEntries.Add(MakeEntry(() => device.IosInProgress, metricIosInProgress.Create(MakeTags([new("name", device.Name)]))));
            updateEntries.Add(MakeEntry(() => device.IoTime, metricIoTime.Create(MakeTags([new("name", device.Name)]))));
            updateEntries.Add(MakeEntry(() => device.WeightIoTime, metricWeightIoTime.Create(MakeTags([new("name", device.Name)]))));
        }
    }

    //--------------------------------------------------------------------------------
    // FileDescriptor
    //--------------------------------------------------------------------------------

    private void SetupFileDescriptorMetric(IMetricManager manager)
    {
        var fd = PlatformProvider.GetFileHandleStat();

        prepareEntries.Add(() => fd.Update());

        var metricAllocated = manager.CreateGauge("system_fd_allocated");
        updateEntries.Add(MakeEntry(() => fd.Allocated, metricAllocated.Create(MakeTags())));

        var metricUsed = manager.CreateGauge("system_fd_used");
        updateEntries.Add(MakeEntry(() => fd.Used, metricUsed.Create(MakeTags())));
    }

    //--------------------------------------------------------------------------------
    // NetworkStat
    //--------------------------------------------------------------------------------

    private void SetupNetworkStatMetric(IMetricManager manager)
    {
        var network = PlatformProvider.GetNetworkStat();

        prepareEntries.Add(() => network.Update());

        var metricBytes = manager.CreateCounter("system_network_bytes_total");
        var metricPackets = manager.CreateCounter("system_network_packets_total");
        var metricErrors = manager.CreateCounter("system_network_errors_total");
        var metricDropped = manager.CreateCounter("system_network_dropped_total");
        var metricFifo = manager.CreateCounter("system_network_fifo_total");
        var metricCompressed = manager.CreateCounter("system_network_compressed_total");
        var metricFrame = manager.CreateCounter("system_network_frame_total");
        var metricMulticast = manager.CreateCounter("system_network_multicast_total");
        var metricCollisions = manager.CreateCounter("system_network_collisions_total");
        var metricCarrier = manager.CreateCounter("system_network_carrier_total");

        foreach (var nif in network.Interfaces)
        {
            updateEntries.Add(MakeEntry(() => nif.RxBytes, metricBytes.Create(MakeTags(new("name", nif.Interface), new("type", "rx")))));
            updateEntries.Add(MakeEntry(() => nif.RxBytes, metricPackets.Create(MakeTags(new("name", nif.Interface), new("type", "rx")))));
            updateEntries.Add(MakeEntry(() => nif.RxErrors, metricErrors.Create(MakeTags(new("name", nif.Interface), new("type", "rx")))));
            updateEntries.Add(MakeEntry(() => nif.RxDropped, metricDropped.Create(MakeTags(new("name", nif.Interface), new("type", "rx")))));
            updateEntries.Add(MakeEntry(() => nif.RxFifo, metricFifo.Create(MakeTags(new("name", nif.Interface), new("type", "rx")))));
            updateEntries.Add(MakeEntry(() => nif.RxCompressed, metricCompressed.Create(MakeTags(new("name", nif.Interface), new("type", "rx")))));
            updateEntries.Add(MakeEntry(() => nif.RxFrame, metricFrame.Create(MakeTags(new("name", nif.Interface), new("type", "rx")))));
            updateEntries.Add(MakeEntry(() => nif.RxMulticast, metricMulticast.Create(MakeTags(new("name", nif.Interface), new("type", "rx")))));

            updateEntries.Add(MakeEntry(() => nif.TxBytes, metricBytes.Create(MakeTags(new("name", nif.Interface), new("type", "tx")))));
            updateEntries.Add(MakeEntry(() => nif.TxBytes, metricPackets.Create(MakeTags(new("name", nif.Interface), new("type", "tx")))));
            updateEntries.Add(MakeEntry(() => nif.TxErrors, metricErrors.Create(MakeTags(new("name", nif.Interface), new("type", "tx")))));
            updateEntries.Add(MakeEntry(() => nif.TxDropped, metricDropped.Create(MakeTags(new("name", nif.Interface), new("type", "tx")))));
            updateEntries.Add(MakeEntry(() => nif.TxFifo, metricFifo.Create(MakeTags(new("name", nif.Interface), new("type", "tx")))));
            updateEntries.Add(MakeEntry(() => nif.TxCompressed, metricCompressed.Create(MakeTags(new("name", nif.Interface), new("type", "tx")))));
            updateEntries.Add(MakeEntry(() => nif.TxCollisions, metricCollisions.Create(MakeTags(new("name", nif.Interface), new("type", "tx")))));
            updateEntries.Add(MakeEntry(() => nif.TxCarrier, metricCarrier.Create(MakeTags(new("name", nif.Interface), new("type", "tx")))));
        }
    }

    //--------------------------------------------------------------------------------
    // Tcp/Tcp6
    //--------------------------------------------------------------------------------

    private void SetupTcpStaticMetric(IMetricManager manager, bool useTcp4, bool useTcp6)
    {
        var metric = manager.CreateGauge("system_tcp_statics");

        if (useTcp4)
        {
            var tcp = PlatformProvider.GetTcpStat();

            prepareEntries.Add(() => tcp.Update());

            SetupTcpStaticEntries(tcp, 4);
        }

        if (useTcp6)
        {
            var tcp6 = PlatformProvider.GetTcp6Stat();

            prepareEntries.Add(() => tcp6.Update());

            SetupTcpStaticEntries(tcp6, 6);
        }

        void SetupTcpStaticEntries(TcpStat stat, int version)
        {
            updateEntries.Add(MakeEntry(() => stat.Established, metric.Create(MakeTags(new("version", version), new("state", "established")))));
            updateEntries.Add(MakeEntry(() => stat.SynSent, metric.Create(MakeTags(new("version", version), new("state", "syn_sent")))));
            updateEntries.Add(MakeEntry(() => stat.SynRecv, metric.Create(MakeTags(new("version", version), new("state", "syn_recv")))));
            updateEntries.Add(MakeEntry(() => stat.FinWait1, metric.Create(MakeTags(new("version", version), new("state", "fin_wait1")))));
            updateEntries.Add(MakeEntry(() => stat.FinWait2, metric.Create(MakeTags(new("version", version), new("state", "fin_wait2")))));
            updateEntries.Add(MakeEntry(() => stat.TimeWait, metric.Create(MakeTags(new("version", version), new("state", "time_wait")))));
            updateEntries.Add(MakeEntry(() => stat.Close, metric.Create(MakeTags(new("version", version), new("state", "close")))));
            updateEntries.Add(MakeEntry(() => stat.CloseWait, metric.Create(MakeTags(new("version", version), new("state", "close_wait")))));
            updateEntries.Add(MakeEntry(() => stat.LastAck, metric.Create(MakeTags(new("version", version), new("state", "last_ack")))));
            updateEntries.Add(MakeEntry(() => stat.Listen, metric.Create(MakeTags(new("version", version), new("state", "listen")))));
            updateEntries.Add(MakeEntry(() => stat.Closing, metric.Create(MakeTags(new("version", version), new("state", "closing")))));
        }
    }

    //--------------------------------------------------------------------------------
    // Wireless
    //--------------------------------------------------------------------------------

    private void SetupWirelessStatMetric(IMetricManager manager)
    {
        var wireless = PlatformProvider.GetWirelessStat();

        prepareEntries.Add(() => wireless.Update());

        var metricLinqQuality = manager.CreateGauge("system_wireless_linq_quality");
        var metricSignalLevel = manager.CreateGauge("system_wireless_signal_level");
        var metricNoiseLevel = manager.CreateGauge("system_wireless_noise_level");
        var metricDiscardPacket = manager.CreateCounter("system_wireless_discard_packet_total");
        var metricMissedBeacon = manager.CreateCounter("system_wireless_missed_beacon_total");

        foreach (var wif in wireless.Interfaces)
        {
            // ReSharper disable StringLiteralTypo
            updateEntries.Add(MakeEntry(() => wif.LinkQuality, metricLinqQuality.Create(MakeTags([new("name", wif.Interface)]))));
            updateEntries.Add(MakeEntry(() => wif.SignalLevel, metricSignalLevel.Create(MakeTags([new("name", wif.Interface)]))));
            updateEntries.Add(MakeEntry(() => wif.NoiseLevel, metricNoiseLevel.Create(MakeTags([new("name", wif.Interface)]))));
            updateEntries.Add(MakeEntry(() => wif.DiscardedNetworkId, metricDiscardPacket.Create(MakeTags(new("name", wif.Interface), new("packet", "nwid")))));
            updateEntries.Add(MakeEntry(() => wif.DiscardedCrypt, metricDiscardPacket.Create(MakeTags(new("name", wif.Interface), new("packet", "crypt")))));
            updateEntries.Add(MakeEntry(() => wif.DiscardedFragment, metricDiscardPacket.Create(MakeTags(new("name", wif.Interface), new("packet", "frag")))));
            updateEntries.Add(MakeEntry(() => wif.DiscardedRetry, metricDiscardPacket.Create(MakeTags(new("name", wif.Interface), new("packet", "retry")))));
            updateEntries.Add(MakeEntry(() => wif.DiscardedMisc, metricDiscardPacket.Create(MakeTags(new("name", wif.Interface), new("packet", "misc")))));
            updateEntries.Add(MakeEntry(() => wif.MissedBeacon, metricMissedBeacon.Create(MakeTags([new("name", wif.Interface)]))));
            // ReSharper restore StringLiteralTypo
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
    // Cpu
    //--------------------------------------------------------------------------------

    private void SetupCpuMetric(IMetricManager manager)
    {
        var cpu = PlatformProvider.GetCpuDevice();

        prepareEntries.Add(() => cpu.Update());

        var metricFrequency = manager.CreateGauge("hardware_cpu_frequency");
        foreach (var core in cpu.Cores)
        {
            updateEntries.Add(MakeEntry(() => core.Frequency, metricFrequency.Create(MakeTags([new("name", core.Name)]))));
        }

        if (cpu.Powers.Count > 0)
        {
            var metricPower = manager.CreateGauge("hardware_cpu_power");
            foreach (var power in cpu.Powers)
            {
                updateEntries.Add(MakeEntry(() => power.Energy / 1000.0, metricPower.Create(MakeTags([new("name", power.Name)]))));
            }
        }
    }

    //--------------------------------------------------------------------------------
    // Battery
    //--------------------------------------------------------------------------------

    private void SetupBatteryMetric(IMetricManager manager)
    {
        var battery = PlatformProvider.GetBatteryDevice();
        if (!battery.Supported)
        {
            return;
        }

        prepareEntries.Add(() => battery.Update());

        var metricCapacity = manager.CreateGauge("hardware_battery_capacity");
        updateEntries.Add(MakeEntry(() => battery.Capacity, metricCapacity.Create(MakeTags())));

        var metricVoltage = manager.CreateGauge("hardware_battery_voltage");
        updateEntries.Add(MakeEntry(() => battery.Voltage / 1000.0, metricVoltage.Create(MakeTags())));

        var metricCurrent = manager.CreateGauge("hardware_battery_current");
        updateEntries.Add(MakeEntry(() => battery.Current / 1000.0, metricCurrent.Create(MakeTags())));

        var metricCharge = manager.CreateGauge("hardware_battery_charge");
        updateEntries.Add(MakeEntry(() => battery.Charge / 1000.0, metricCharge.Create(MakeTags())));

        var metricChargeFull = manager.CreateGauge("hardware_battery_charge_full");
        updateEntries.Add(MakeEntry(() => battery.ChargeFull / 1000.0, metricChargeFull.Create(MakeTags())));
    }

    //--------------------------------------------------------------------------------
    // Mains
    //--------------------------------------------------------------------------------

    private void SetupMainsMetric(IMetricManager manager)
    {
        var adapter = PlatformProvider.GetMainsDevice();
        if (!adapter.Supported)
        {
            return;
        }

        prepareEntries.Add(() => adapter.Update());

        var metric = manager.CreateGauge("hardware_ac_online");
        updateEntries.Add(MakeEntry(() => adapter.Online ? 1 : 0, metric.Create(MakeTags())));
    }

    //--------------------------------------------------------------------------------
    // HardwareMonitor
    //--------------------------------------------------------------------------------

    private void SetupHardwareMonitorMetric(IMetricManager manager)
    {
        var monitors = PlatformProvider.GetHardwareMonitors();

        prepareEntries.Add(() =>
        {
            foreach (var monitor in monitors)
            {
                foreach (var sensor in monitor.Sensors)
                {
                    sensor.Update();
                }
            }
        });

        var metric = manager.CreateGauge("hardware_monitor");

        foreach (var monitor in monitors)
        {
            foreach (var sensor in monitor.Sensors)
            {
                updateEntries.Add(MakeEntry(() => sensor.Value, metric.Create(MakeTags(new("name", monitor.Name), new("sensor", sensor.Type), new("type", monitor.Type), new("label", sensor.Label)))));
            }
        }
    }
}
