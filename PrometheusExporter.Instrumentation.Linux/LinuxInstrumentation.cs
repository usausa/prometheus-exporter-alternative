namespace PrometheusExporter.Instrumentation.Linux;

using System;

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
        if (options.Memory)
        {
            SetupMemoryMetric(manager);
        }
        if (options.VirtualMemory)
        {
            SetupVirtualMemoryMetric(manager);
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

    // TODO delete
    // ReSharper disable UnusedVariable
    // ReSharper disable UnusedParameter.Local
#pragma warning disable CA1822

    private void SetupStaticsMetric(IMetricManager manager)
    {
        var statics = PlatformProvider.GetStatics();

        prepareEntries.Add(() => statics.Update());

        // TODO
        //Console.WriteLine($"Interrupt:      {statics.Interrupt}");
        //Console.WriteLine($"ContextSwitch:  {statics.ContextSwitch}");
        //Console.WriteLine($"SoftIrq:        {statics.SoftIrq}");
        //Console.WriteLine($"ProcessRunning: {statics.ProcessRunning}");
        //Console.WriteLine($"ProcessBlocked: {statics.ProcessBlocked}");

        //Console.WriteLine($"User:           {statics.CpuTotal.User}");
        //Console.WriteLine($"Nice:           {statics.CpuTotal.Nice}");
        //Console.WriteLine($"System:         {statics.CpuTotal.System}");
        //Console.WriteLine($"Idle:           {statics.CpuTotal.Idle}");
        //Console.WriteLine($"IoWait:         {statics.CpuTotal.IoWait}");
        //Console.WriteLine($"Irq:            {statics.CpuTotal.Irq}");
        //Console.WriteLine($"SoftIrq:        {statics.CpuTotal.SoftIrq}");
        //Console.WriteLine($"Steal:          {statics.CpuTotal.Steal}");
        //Console.WriteLine($"Guest:          {statics.CpuTotal.Guest}");
        //Console.WriteLine($"GuestNice:      {statics.CpuTotal.GuestNice}");
    }

    private void SetupLoadAverageMetric(IMetricManager manager)
    {
        var load = PlatformProvider.GetLoadAverage();

        prepareEntries.Add(() => load.Update());

        var metric = manager.CreateMetric("system_load_average");
        updateEntries.Add(new Entry(() => load.Average1, metric.CreateGauge(MakeTags([new("window", 1)]))));
        updateEntries.Add(new Entry(() => load.Average5, metric.CreateGauge(MakeTags([new("window", 5)]))));
        updateEntries.Add(new Entry(() => load.Average15, metric.CreateGauge(MakeTags([new("window", 15)]))));
    }

    private void SetupMemoryMetric(IMetricManager manager)
    {
        var memory = PlatformProvider.GetMemory();

        prepareEntries.Add(() => memory.Update());

        // TODO
        //Console.WriteLine($"Total:   {memory.Total}");
        //Console.WriteLine($"Free:    {memory.Free}");
        //Console.WriteLine($"Buffers: {memory.Buffers}");
        //Console.WriteLine($"Cached:  {memory.Cached}");
        //Console.WriteLine($"Usage:   {(int)Math.Ceiling(memory.Usage)}");
    }

    private void SetupVirtualMemoryMetric(IMetricManager manager)
    {
        var vm = PlatformProvider.GetVirtualMemory();

        prepareEntries.Add(() => vm.Update());

        // TODO
        //Console.WriteLine($"PageIn:            {vm.PageIn}");
        //Console.WriteLine($"PageOut:           {vm.PageOut}");
        //Console.WriteLine($"SwapIn:            {vm.SwapIn}");
        //Console.WriteLine($"SwapOut:           {vm.SwapOut}");
        //Console.WriteLine($"PageFault:         {vm.PageFault}");
        //Console.WriteLine($"MajorPageFault:    {vm.MajorPageFault}");
        //Console.WriteLine($"OutOfMemoryKiller: {vm.OutOfMemoryKiller}");
    }

    private void SetupPartitionMetric(IMetricManager manager)
    {
        var partitions = PlatformProvider.GetPartitions();

        //prepareEntries.Add(() => partitions.Update());

        // TODO
        //foreach (var partition in partitions)
        //{
        //    var drive = new DriveInfo(partition.MountPoints[0]);
        //    var used = drive.TotalSize - drive.TotalFreeSpace;
        //    var available = drive.AvailableFreeSpace;
        //    var usage = (int)Math.Ceiling((double)used / (available + used) * 100);

        //    Console.WriteLine($"Name:          {partition.Name}");
        //    Console.WriteLine($"MountPoint:    {String.Join(' ', partition.MountPoints)}");
        //    Console.WriteLine($"TotalSize:     {drive.TotalSize / 1024}");
        //    Console.WriteLine($"UsedSize:      {used / 1024}");
        //    Console.WriteLine($"AvailableSize: {available / 1024}");
        //    Console.WriteLine($"Usage:         {usage}");
        //}
    }

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

    private void SetupFileDescriptorMetric(IMetricManager manager)
    {
        var fd = PlatformProvider.GetFileDescriptor();

        prepareEntries.Add(() => fd.Update());

        var metricAllocated = manager.CreateMetric("system_fd_allocated");
        updateEntries.Add(new Entry(() => fd.Allocated, metricAllocated.CreateGauge(MakeTags())));

        var metricUsed = manager.CreateMetric("system_fd_used");
        updateEntries.Add(new Entry(() => fd.Used, metricUsed.CreateGauge(MakeTags())));
    }

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

    private void SetupProcessSummaryMetric(IMetricManager manager)
    {
        var process = PlatformProvider.GetProcessSummary();

        prepareEntries.Add(() => process.Update());

        var metricProcess = manager.CreateMetric("system_process_count");
        updateEntries.Add(new Entry(() => process.ProcessCount, metricProcess.CreateGauge(MakeTags())));

        var metricThread = manager.CreateMetric("system_thread_count");
        updateEntries.Add(new Entry(() => process.ThreadCount, metricThread.CreateGauge(MakeTags())));
    }

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
                updateEntries.Add(new Entry(() => power.Energy / 1000.0, metricFrequency.CreateGauge(MakeTags([new("name", power.Name)]))));
            }
        }
    }

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
}
