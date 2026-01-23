namespace PrometheusExporter.Instrumentation.Linux;

using LinuxDotNet.SystemInfo;

using PrometheusExporter.Abstractions;

internal sealed class LinuxInstrumentation
{
    private readonly string host;

    private readonly TimeSpan updateDuration;

    private readonly List<Action> prepareEntries = [];

    private readonly List<Entry> updateEntries = [];

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
        if (options.Statics)
        {
            SetupStaticsMetric(manager);
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
        if (options.Partition)
        {
            SetupPartitionMetric(manager);
        }
        if (options.DiskStatics)
        {
            SetupDiskStaticsMetric(manager);
        }
        if (options.FileDescriptor)
        {
            SetupFileDescriptorMetric(manager);
        }
        if (options.NetworkStatic)
        {
            SetupNetworkStaticMetric(manager);
        }
        if (options.TcpStatic || options.Tcp6Static)
        {
            SetupTcpStaticMetric(manager, options.TcpStatic, options.Tcp6Static);
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
        if (options.MainsAdapter)
        {
            SetupMainsAdapterMetric(manager);
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

        foreach (var entry in updateEntries)
        {
            entry.Update();
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

    //--------------------------------------------------------------------------------
    // Uptime
    //--------------------------------------------------------------------------------

    private void SetupUptimeMetric(IMetricManager manager)
    {
        // Uptime
        var uptimeInfo = PlatformProvider.GetUptime();

        prepareEntries.Add(() => uptimeInfo.Update());

        var metric = manager.CreateMetric("system_uptime");
        updateEntries.Add(new Entry(() => uptimeInfo.Uptime.TotalSeconds, metric.CreateGauge(MakeTags())));
    }

    //--------------------------------------------------------------------------------
    // Statics
    //--------------------------------------------------------------------------------

    private void SetupStaticsMetric(IMetricManager manager)
    {
        var statics = PlatformProvider.GetStatics();

        var totalPrevious = new PreviousCpuTotal();
        var corePrevious = new PreviousCpuTotal[statics.CpuCores.Count];
        for (var i = 0; i < corePrevious.Length; i++)
        {
            corePrevious[i] = new PreviousCpuTotal();
        }

        prepareEntries.Add(() =>
        {
            UpdatePrevious(totalPrevious, statics.CpuTotal);
            for (var i = 0; i < corePrevious.Length; i++)
            {
                UpdatePrevious(corePrevious[i], statics.CpuCores[i]);
            }

            statics.Update();
        });

        var metricInterrupt = manager.CreateMetric("system_interrupt_total");
        var metricContextSwitch = manager.CreateMetric("system_context_switch_total");
        var metricForks = manager.CreateMetric("system_forks_total");
        var metricScheduler = manager.CreateMetric("system_scheduler_task");
        var metricSoftIrq = manager.CreateMetric("system_softirq_total");

        updateEntries.Add(new Entry(() => statics.Interrupt, metricInterrupt.CreateGauge(MakeTags())));
        updateEntries.Add(new Entry(() => statics.ContextSwitch, metricContextSwitch.CreateGauge(MakeTags())));
        updateEntries.Add(new Entry(() => statics.Forks, metricForks.CreateGauge(MakeTags())));
        updateEntries.Add(new Entry(() => statics.RunnableTasks, metricScheduler.CreateGauge(MakeTags([new("state", "runnable")]))));
        updateEntries.Add(new Entry(() => statics.BlockedTasks, metricScheduler.CreateGauge(MakeTags([new("state", "blocked")]))));
        updateEntries.Add(new Entry(() => statics.SoftIrq, metricSoftIrq.CreateGauge(MakeTags())));

        var metricCpuTimeTotal = manager.CreateMetric("system_cpu_time_total");

        foreach (var cpu in statics.CpuCores)
        {
            SetupCpuTimeEntries(cpu);
        }
        SetupCpuTimeEntries(statics.CpuTotal);

        var metricCpuLoad = manager.CreateMetric("system_cpu_load");

        for (var i = 0; i < corePrevious.Length; i++)
        {
            SetupCpuLoadEntries(corePrevious[i], statics.CpuCores[i]);
        }
        SetupCpuLoadEntries(totalPrevious, statics.CpuTotal);

        return;

        void SetupCpuLoadEntries(PreviousCpuTotal previous, CpuStatics cpu)
        {
            updateEntries.Add(new Entry(() =>
            {
                var nonIdle = CalcCpuNonIdle(cpu);
                var idle = CalcCpuIdle(cpu);
                var total = idle + nonIdle;

                var totalDiff = total - previous.Total;
                var nonIdleDiff = nonIdle - previous.NonIdle;
                return totalDiff == 0 ? 0 : (double)nonIdleDiff / totalDiff * 100.0;
            }, metricCpuLoad.CreateGauge(MakeTags([new("name", cpu.Name)]))));
        }

        void SetupCpuTimeEntries(CpuStatics cpu)
        {
            // ReSharper disable StringLiteralTypo
            updateEntries.Add(new Entry(() => cpu.User, metricCpuTimeTotal.CreateGauge(MakeTags(new("name", cpu.Name), new("mode", "user")))));
            updateEntries.Add(new Entry(() => cpu.Nice, metricCpuTimeTotal.CreateGauge(MakeTags(new("name", cpu.Name), new("mode", "nice")))));
            updateEntries.Add(new Entry(() => cpu.System, metricCpuTimeTotal.CreateGauge(MakeTags(new("name", cpu.Name), new("mode", "system")))));
            updateEntries.Add(new Entry(() => cpu.Idle, metricCpuTimeTotal.CreateGauge(MakeTags(new("name", cpu.Name), new("mode", "idle")))));
            updateEntries.Add(new Entry(() => cpu.IoWait, metricCpuTimeTotal.CreateGauge(MakeTags(new("name", cpu.Name), new("mode", "iowait")))));
            updateEntries.Add(new Entry(() => cpu.Irq, metricCpuTimeTotal.CreateGauge(MakeTags(new("name", cpu.Name), new("mode", "irq")))));
            updateEntries.Add(new Entry(() => cpu.SoftIrq, metricCpuTimeTotal.CreateGauge(MakeTags(new("name", cpu.Name), new("mode", "softirq")))));
            updateEntries.Add(new Entry(() => cpu.Steal, metricCpuTimeTotal.CreateGauge(MakeTags(new("name", cpu.Name), new("mode", "steal")))));
            updateEntries.Add(new Entry(() => cpu.Guest, metricCpuTimeTotal.CreateGauge(MakeTags(new("name", cpu.Name), new("mode", "guest")))));
            updateEntries.Add(new Entry(() => cpu.GuestNice, metricCpuTimeTotal.CreateGauge(MakeTags(new("name", cpu.Name), new("mode", "guestnice")))));
            // ReSharper restore StringLiteralTypo
        }

        static void UpdatePrevious(PreviousCpuTotal previous, CpuStatics cpu)
        {
            var nonIdle = CalcCpuNonIdle(cpu);
            var idle = CalcCpuIdle(cpu);
            previous.NonIdle = nonIdle;
            previous.Total = idle + nonIdle;
        }

        static long CalcCpuIdle(CpuStatics cpu)
        {
            return cpu.Idle + cpu.IoWait;
        }

        static long CalcCpuNonIdle(CpuStatics cpu)
        {
            return cpu.User + cpu.Nice + cpu.System + cpu.Irq + cpu.SoftIrq + cpu.Steal;
        }
    }

    //--------------------------------------------------------------------------------
    // LoadAverage
    //--------------------------------------------------------------------------------

    private void SetupLoadAverageMetric(IMetricManager manager)
    {
        var load = PlatformProvider.GetLoadAverage();

        prepareEntries.Add(() => load.Update());

        var metric = manager.CreateMetric("system_load_average");
        updateEntries.Add(new Entry(() => load.Average1, metric.CreateGauge(MakeTags([new("window", 1)]))));
        updateEntries.Add(new Entry(() => load.Average5, metric.CreateGauge(MakeTags([new("window", 5)]))));
        updateEntries.Add(new Entry(() => load.Average15, metric.CreateGauge(MakeTags([new("window", 15)]))));
    }

    //--------------------------------------------------------------------------------
    // Memory
    //--------------------------------------------------------------------------------

    private void SetupMemoryMetric(IMetricManager manager, string[] targets)
    {
        var memory = PlatformProvider.GetMemory();

        prepareEntries.Add(() => memory.Update());

        // ReSharper disable StringLiteralTypo
        SetupCustomMetrics("load", metric =>
        [
            new Entry(() => (double)(memory.MemoryTotal - memory.MemoryAvailable) / memory.MemoryTotal * 100, metric.CreateGauge(MakeTags()))
        ]);
        SetupCustomMetrics("mem", metric =>
        [
            new Entry(() => memory.MemoryTotal, metric.CreateGauge(MakeTags([new("type", "total")]))),
            new Entry(() => memory.MemoryAvailable, metric.CreateGauge(MakeTags([new("type", "available")]))),
            new Entry(() => memory.MemoryFree, metric.CreateGauge(MakeTags([new("type", "free")])))
        ]);
        SetupSimpleMetrics("buffers", () => memory.Buffers);
        SetupSimpleMetrics("cached", () => memory.Cached);
        SetupSimpleMetrics("swap_cached", () => memory.SwapCached);
        SetupCustomMetrics("lru", metric =>
        [
            new Entry(() => memory.ActiveAnonymous, metric.CreateGauge(MakeTags(new("type", "anon"), new("state", "active")))),
            new Entry(() => memory.InactiveAnonymous, metric.CreateGauge(MakeTags(new("type", "anon"), new("state", "inactive")))),
            new Entry(() => memory.ActiveFile, metric.CreateGauge(MakeTags(new("type", "file"), new("state", "active")))),
            new Entry(() => memory.InactiveFile, metric.CreateGauge(MakeTags(new("type", "file"), new("state", "inactive"))))
        ]);
        SetupSimpleMetrics("unevictable", () => memory.Unevictable);
        SetupSimpleMetrics("mlocked", () => memory.MemoryLocked);
        SetupCustomMetrics("swap", metric =>
        [
            new Entry(() => memory.SwapTotal, metric.CreateGauge(MakeTags([new("type", "total")]))),
            new Entry(() => memory.SwapFree, metric.CreateGauge(MakeTags([new("type", "free")])))
        ]);
        SetupSimpleMetrics("dirty", () => memory.Dirty);
        SetupSimpleMetrics("writeback", () => memory.Writeback);
        SetupSimpleMetrics("anon_pages", () => memory.AnonymousPages);
        SetupSimpleMetrics("mapped", () => memory.Mapped);
        SetupSimpleMetrics("shmem", () => memory.SharedMemory);
        SetupSimpleMetrics("k_reclaimable", () => memory.KernelReclaimable);
        SetupCustomMetrics("slab", metric =>
        [
            new Entry(() => memory.SlabTotal, metric.CreateGauge(MakeTags([new("type", "total")]))),
            new Entry(() => memory.SlabReclaimable, metric.CreateGauge(MakeTags([new("type", "reclaimable")]))),
            new Entry(() => memory.SlabUnreclaimable, metric.CreateGauge(MakeTags([new("type", "unreclaimable")])))
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
                var metric = manager.CreateMetric($"system_memory_{name}");
                updateEntries.Add(new Entry(selector, metric.CreateGauge(MakeTags())));
            }
        }

        void SetupCustomMetrics(string name, Func<IMetric, Entry[]> func)
        {
            if (IsTarget(targets, name))
            {
                var metric = manager.CreateMetric($"system_memory_{name}");
                updateEntries.AddRange(func(metric));
            }
        }
    }

    //--------------------------------------------------------------------------------
    // VirtualMemory
    //--------------------------------------------------------------------------------

    private void SetupVirtualMemoryMetric(IMetricManager manager, string[] targets)
    {
        var vm = PlatformProvider.GetVirtualMemory();

        prepareEntries.Add(() => vm.Update());

        SetupCustomMetrics("page", metric =>
        [
            new Entry(() => vm.PageIn, metric.CreateGauge(MakeTags([new("type", "in")]))),
            new Entry(() => vm.PageOut, metric.CreateGauge(MakeTags([new("type", "out")])))
        ]);
        SetupCustomMetrics("swap", metric =>
        [
            new Entry(() => vm.SwapIn, metric.CreateGauge(MakeTags([new("type", "in")]))),
            new Entry(() => vm.SwapOut, metric.CreateGauge(MakeTags([new("type", "out")])))
        ]);
        SetupCustomMetrics("page_faults", metric =>
        [
            new Entry(() => vm.PageFaults, metric.CreateGauge(MakeTags([new("type", "in")]))),
            new Entry(() => vm.MajorPageFaults, metric.CreateGauge(MakeTags([new("type", "out")])))
        ]);
        SetupCustomMetrics("steal", metric =>
        [
            new Entry(() => vm.StealKernel, metric.CreateGauge(MakeTags([new("type", "kernel")]))),
            new Entry(() => vm.StealDirect, metric.CreateGauge(MakeTags([new("type", "direct")])))
        ]);
        SetupCustomMetrics("scan", metric =>
        [
            new Entry(() => vm.ScanKernel, metric.CreateGauge(MakeTags([new("type", "kernel")]))),
            new Entry(() => vm.ScanDirect, metric.CreateGauge(MakeTags([new("type", "direct")])))
        ]);
        SetupSimpleMetrics("oom_kill", () => vm.OutOfMemoryKiller);

        void SetupSimpleMetrics(string name, Func<double> selector)
        {
            if (IsTarget(targets, name))
            {
                var metric = manager.CreateMetric($"system_virtual_{name}_total");
                updateEntries.Add(new Entry(selector, metric.CreateGauge(MakeTags())));
            }
        }

        void SetupCustomMetrics(string name, Func<IMetric, Entry[]> func)
        {
            if (IsTarget(targets, name))
            {
                var metric = manager.CreateMetric($"system_virtual_{name}_total");
                updateEntries.AddRange(func(metric));
            }
        }
    }

    //--------------------------------------------------------------------------------
    // Partition
    //--------------------------------------------------------------------------------

    private void SetupPartitionMetric(IMetricManager manager)
    {
        var partitions = PlatformProvider.GetPartitions();
        var drives = partitions.Select(static x => new DriveInfo(x.MountPoints[0])).ToList();

        var metricUsed = manager.CreateMetric("system_partition_used");
        foreach (var drive in drives)
        {
            updateEntries.Add(new Entry(() => (double)(drive.TotalSize - drive.TotalFreeSpace) / drive.TotalSize * 100, metricUsed.CreateGauge([new("name", drive.Name)])));
        }

        var metricTotal = manager.CreateMetric("system_partition_total");
        foreach (var drive in drives)
        {
            updateEntries.Add(new Entry(() => drive.TotalSize, metricTotal.CreateGauge([new("name", drive.Name)])));
        }

        var metricFree = manager.CreateMetric("system_partition_free");
        foreach (var drive in drives)
        {
            updateEntries.Add(new Entry(() => drive.TotalFreeSpace, metricFree.CreateGauge([new("name", drive.Name)])));
        }
    }

    //--------------------------------------------------------------------------------
    // DiskStatics
    //--------------------------------------------------------------------------------

    private void SetupDiskStaticsMetric(IMetricManager manager)
    {
        var disk = PlatformProvider.GetDiskStatics();

        prepareEntries.Add(() => disk.Update());

        var metricCompleted = manager.CreateMetric("system_disk_completed_total");
        var metricMerged = manager.CreateMetric("system_disk_merged_total");
        var metricSectors = manager.CreateMetric("system_disk_sectors_total");
        var metricTime = manager.CreateMetric("system_disk_time_total");
        var metricIosInProgress = manager.CreateMetric("system_disk_ios_in_progress");
        var metricIoTime = manager.CreateMetric("system_disk_io_time_total");
        var metricWeightIoTime = manager.CreateMetric("system_disk_weight_io_time_total");

        foreach (var device in disk.Devices)
        {
            updateEntries.Add(new Entry(() => device.ReadCompleted, metricCompleted.CreateGauge(MakeTags(new("name", device.Name), new("type", "read")))));
            updateEntries.Add(new Entry(() => device.ReadMerged, metricMerged.CreateGauge(MakeTags(new("name", device.Name), new("type", "read")))));
            updateEntries.Add(new Entry(() => device.ReadSectors, metricSectors.CreateGauge(MakeTags(new("name", device.Name), new("type", "read")))));
            updateEntries.Add(new Entry(() => device.ReadTime, metricTime.CreateGauge(MakeTags(new("name", device.Name), new("type", "read")))));

            updateEntries.Add(new Entry(() => device.WriteCompleted, metricCompleted.CreateGauge(MakeTags(new("name", device.Name), new("type", "write")))));
            updateEntries.Add(new Entry(() => device.WriteMerged, metricMerged.CreateGauge(MakeTags(new("name", device.Name), new("type", "write")))));
            updateEntries.Add(new Entry(() => device.WriteSectors, metricSectors.CreateGauge(MakeTags(new("name", device.Name), new("type", "write")))));
            updateEntries.Add(new Entry(() => device.WriteTime, metricTime.CreateGauge(MakeTags(new("name", device.Name), new("type", "write")))));

            updateEntries.Add(new Entry(() => device.IosInProgress, metricIosInProgress.CreateGauge(MakeTags([new("name", device.Name)]))));
            updateEntries.Add(new Entry(() => device.IoTime, metricIoTime.CreateGauge(MakeTags([new("name", device.Name)]))));
            updateEntries.Add(new Entry(() => device.WeightIoTime, metricWeightIoTime.CreateGauge(MakeTags([new("name", device.Name)]))));
        }
    }

    //--------------------------------------------------------------------------------
    // FileDescriptor
    //--------------------------------------------------------------------------------

    private void SetupFileDescriptorMetric(IMetricManager manager)
    {
        var fd = PlatformProvider.GetFileDescriptor();

        prepareEntries.Add(() => fd.Update());

        var metricAllocated = manager.CreateMetric("system_fd_allocated");
        updateEntries.Add(new Entry(() => fd.Allocated, metricAllocated.CreateGauge(MakeTags())));

        var metricUsed = manager.CreateMetric("system_fd_used");
        updateEntries.Add(new Entry(() => fd.Used, metricUsed.CreateGauge(MakeTags())));
    }

    //--------------------------------------------------------------------------------
    // NetworkStatic
    //--------------------------------------------------------------------------------

    private void SetupNetworkStaticMetric(IMetricManager manager)
    {
        var network = PlatformProvider.GetNetworkStatic();

        prepareEntries.Add(() => network.Update());

        var metricBytes = manager.CreateMetric("system_network_bytes_total");
        var metricPackets = manager.CreateMetric("system_network_packets_total");
        var metricErrors = manager.CreateMetric("system_network_errors_total");
        var metricDropped = manager.CreateMetric("system_network_dropped_total");
        var metricFifo = manager.CreateMetric("system_network_fifo_total");
        var metricCompressed = manager.CreateMetric("system_network_compressed_total");
        var metricFrame = manager.CreateMetric("system_network_frame_total");
        var metricMulticast = manager.CreateMetric("system_network_multicast_total");
        var metricCollisions = manager.CreateMetric("system_network_collisions_total");
        var metricCarrier = manager.CreateMetric("system_network_carrier_total");

        foreach (var nif in network.Interfaces)
        {
            updateEntries.Add(new Entry(() => nif.RxBytes, metricBytes.CreateGauge(MakeTags(new("name", nif.Interface), new("type", "rx")))));
            updateEntries.Add(new Entry(() => nif.RxBytes, metricPackets.CreateGauge(MakeTags(new("name", nif.Interface), new("type", "rx")))));
            updateEntries.Add(new Entry(() => nif.RxErrors, metricErrors.CreateGauge(MakeTags(new("name", nif.Interface), new("type", "rx")))));
            updateEntries.Add(new Entry(() => nif.RxDropped, metricDropped.CreateGauge(MakeTags(new("name", nif.Interface), new("type", "rx")))));
            updateEntries.Add(new Entry(() => nif.RxFifo, metricFifo.CreateGauge(MakeTags(new("name", nif.Interface), new("type", "rx")))));
            updateEntries.Add(new Entry(() => nif.RxCompressed, metricCompressed.CreateGauge(MakeTags(new("name", nif.Interface), new("type", "rx")))));
            updateEntries.Add(new Entry(() => nif.RxFrame, metricFrame.CreateGauge(MakeTags(new("name", nif.Interface), new("type", "rx")))));
            updateEntries.Add(new Entry(() => nif.RxMulticast, metricMulticast.CreateGauge(MakeTags(new("name", nif.Interface), new("type", "rx")))));

            updateEntries.Add(new Entry(() => nif.TxBytes, metricBytes.CreateGauge(MakeTags(new("name", nif.Interface), new("type", "tx")))));
            updateEntries.Add(new Entry(() => nif.TxBytes, metricPackets.CreateGauge(MakeTags(new("name", nif.Interface), new("type", "tx")))));
            updateEntries.Add(new Entry(() => nif.TxErrors, metricErrors.CreateGauge(MakeTags(new("name", nif.Interface), new("type", "tx")))));
            updateEntries.Add(new Entry(() => nif.TxDropped, metricDropped.CreateGauge(MakeTags(new("name", nif.Interface), new("type", "tx")))));
            updateEntries.Add(new Entry(() => nif.TxFifo, metricFifo.CreateGauge(MakeTags(new("name", nif.Interface), new("type", "tx")))));
            updateEntries.Add(new Entry(() => nif.TxCompressed, metricCompressed.CreateGauge(MakeTags(new("name", nif.Interface), new("type", "tx")))));
            updateEntries.Add(new Entry(() => nif.TxCollisions, metricCollisions.CreateGauge(MakeTags(new("name", nif.Interface), new("type", "tx")))));
            updateEntries.Add(new Entry(() => nif.TxCarrier, metricCarrier.CreateGauge(MakeTags(new("name", nif.Interface), new("type", "tx")))));
        }
    }

    //--------------------------------------------------------------------------------
    // Tcp/Tcp6
    //--------------------------------------------------------------------------------

    private void SetupTcpStaticMetric(IMetricManager manager, bool useTcp4, bool useTcp6)
    {
        var metric = manager.CreateMetric("system_tcp_statics");

        if (useTcp4)
        {
            var tcp = PlatformProvider.GetTcp();

            prepareEntries.Add(() => tcp.Update());

            SetupTcpStaticEntries(tcp, 4);
        }

        if (useTcp6)
        {
            var tcp6 = PlatformProvider.GetTcp6();

            prepareEntries.Add(() => tcp6.Update());

            SetupTcpStaticEntries(tcp6, 6);
        }

        void SetupTcpStaticEntries(TcpInfo info, int version)
        {
            updateEntries.Add(new Entry(() => info.Established, metric.CreateGauge(MakeTags(new("version", version), new("state", "established")))));
            updateEntries.Add(new Entry(() => info.SynSent, metric.CreateGauge(MakeTags(new("version", version), new("state", "syn_sent")))));
            updateEntries.Add(new Entry(() => info.SynRecv, metric.CreateGauge(MakeTags(new("version", version), new("state", "syn_recv")))));
            updateEntries.Add(new Entry(() => info.FinWait1, metric.CreateGauge(MakeTags(new("version", version), new("state", "fin_wait1")))));
            updateEntries.Add(new Entry(() => info.FinWait2, metric.CreateGauge(MakeTags(new("version", version), new("state", "fin_wait2")))));
            updateEntries.Add(new Entry(() => info.TimeWait, metric.CreateGauge(MakeTags(new("version", version), new("state", "time_wait")))));
            updateEntries.Add(new Entry(() => info.Close, metric.CreateGauge(MakeTags(new("version", version), new("state", "close")))));
            updateEntries.Add(new Entry(() => info.CloseWait, metric.CreateGauge(MakeTags(new("version", version), new("state", "close_wait")))));
            updateEntries.Add(new Entry(() => info.LastAck, metric.CreateGauge(MakeTags(new("version", version), new("state", "last_ack")))));
            updateEntries.Add(new Entry(() => info.Listen, metric.CreateGauge(MakeTags(new("version", version), new("state", "listen")))));
            updateEntries.Add(new Entry(() => info.Closing, metric.CreateGauge(MakeTags(new("version", version), new("state", "closing")))));
        }
    }

    //--------------------------------------------------------------------------------
    // ProcessSummary
    //--------------------------------------------------------------------------------

    private void SetupProcessSummaryMetric(IMetricManager manager)
    {
        var process = PlatformProvider.GetProcessSummary();

        prepareEntries.Add(() => process.Update());

        var metricProcess = manager.CreateMetric("system_process_count");
        updateEntries.Add(new Entry(() => process.ProcessCount, metricProcess.CreateGauge(MakeTags())));

        var metricThread = manager.CreateMetric("system_thread_count");
        updateEntries.Add(new Entry(() => process.ThreadCount, metricThread.CreateGauge(MakeTags())));
    }

    //--------------------------------------------------------------------------------
    // Cpu
    //--------------------------------------------------------------------------------

    private void SetupCpuMetric(IMetricManager manager)
    {
        var cpu = PlatformProvider.GetCpu();

        prepareEntries.Add(() => cpu.Update());

        var metricFrequency = manager.CreateMetric("hardware_cpu_frequency");
        foreach (var core in cpu.Cores)
        {
            updateEntries.Add(new Entry(() => core.Frequency, metricFrequency.CreateGauge(MakeTags([new("name", core.Name)]))));
        }

        if (cpu.Powers.Count > 0)
        {
            var metricPower = manager.CreateMetric("hardware_cpu_power");
            foreach (var power in cpu.Powers)
            {
                updateEntries.Add(new Entry(() => power.Energy / 1000.0, metricPower.CreateGauge(MakeTags([new("name", power.Name)]))));
            }
        }
    }

    //--------------------------------------------------------------------------------
    // Battery
    //--------------------------------------------------------------------------------

    private void SetupBatteryMetric(IMetricManager manager)
    {
        var battery = PlatformProvider.GetBattery();
        if (!battery.Supported)
        {
            return;
        }

        prepareEntries.Add(() => battery.Update());

        var metricCapacity = manager.CreateMetric("hardware_battery_capacity");
        updateEntries.Add(new Entry(() => battery.Capacity, metricCapacity.CreateGauge(MakeTags())));

        var metricVoltage = manager.CreateMetric("hardware_battery_voltage");
        updateEntries.Add(new Entry(() => battery.Voltage / 1000.0, metricVoltage.CreateGauge(MakeTags())));

        var metricCurrent = manager.CreateMetric("hardware_battery_current");
        updateEntries.Add(new Entry(() => battery.Current / 1000.0, metricCurrent.CreateGauge(MakeTags())));

        var metricCharge = manager.CreateMetric("hardware_battery_charge");
        updateEntries.Add(new Entry(() => battery.Charge / 1000.0, metricCharge.CreateGauge(MakeTags())));

        var metricChargeFull = manager.CreateMetric("hardware_battery_charge_full");
        updateEntries.Add(new Entry(() => battery.ChargeFull / 1000.0, metricChargeFull.CreateGauge(MakeTags())));
    }

    //--------------------------------------------------------------------------------
    // MainsAdapter
    //--------------------------------------------------------------------------------

    private void SetupMainsAdapterMetric(IMetricManager manager)
    {
        var adapter = PlatformProvider.GetMainsAdapter();
        if (!adapter.Supported)
        {
            return;
        }

        prepareEntries.Add(() => adapter.Update());

        var metric = manager.CreateMetric("hardware_ac_online");
        updateEntries.Add(new Entry(() => adapter.Online ? 1 : 0, metric.CreateGauge(MakeTags())));
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

        var metric = manager.CreateMetric("hardware_monitor");

        foreach (var monitor in monitors)
        {
            foreach (var sensor in monitor.Sensors)
            {
                updateEntries.Add(new Entry(() => sensor.Value, metric.CreateGauge(MakeTags(new("name", monitor.Name), new("sensor", sensor.Type), new("type", monitor.Type), new("label", sensor.Label)))));
            }
        }
    }

    //--------------------------------------------------------------------------------
    // Entry
    //--------------------------------------------------------------------------------

    private sealed class Entry
    {
        private readonly Func<double> measurement;

        private readonly IGauge gauge;

        public Entry(Func<double> measurement, IGauge gauge)
        {
            this.measurement = measurement;
            this.gauge = gauge;
        }

        public void Update()
        {
            gauge.Value = measurement();
        }
    }

    private sealed class PreviousCpuTotal
    {
        public long NonIdle { get; set; }

        public long Total { get; set; }
    }
}
