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

        // TODO
        //foreach (var device in disk.Devices)
        //{
        //    Console.WriteLine($"Name:           {device.Name}");
        //    Console.WriteLine($"ReadCompleted:  {device.ReadCompleted}");
        //    Console.WriteLine($"ReadMerged:     {device.ReadMerged}");
        //    Console.WriteLine($"ReadSectors:    {device.ReadSectors}");
        //    Console.WriteLine($"ReadTime:       {device.ReadTime}");
        //    Console.WriteLine($"WriteCompleted: {device.WriteCompleted}");
        //    Console.WriteLine($"WriteMerged:    {device.WriteMerged}");
        //    Console.WriteLine($"WriteSectors:   {device.WriteSectors}");
        //    Console.WriteLine($"WriteTime:      {device.WriteTime}");
        //    Console.WriteLine($"IosInProgress:  {device.IosInProgress}");
        //    Console.WriteLine($"IoTime:         {device.IoTime}");
        //    Console.WriteLine($"WeightIoTime:   {device.WeightIoTime}");
        //}
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

        // TODO n
        //foreach (var nif in network.Interfaces)
        //{
        //    Console.WriteLine($"Interface:    {nif.Interface}");
        //    Console.WriteLine($"RxBytes:      {nif.RxBytes}");
        //    Console.WriteLine($"RxPackets:    {nif.RxPackets}");
        //    Console.WriteLine($"RxErrors:     {nif.RxErrors}");
        //    Console.WriteLine($"RxDropped:    {nif.RxDropped}");
        //    Console.WriteLine($"RxFifo:       {nif.RxFifo}");
        //    Console.WriteLine($"RxFrame:      {nif.RxFrame}");
        //    Console.WriteLine($"RxCompressed: {nif.RxCompressed}");
        //    Console.WriteLine($"RxMulticast:  {nif.RxMulticast}");
        //    Console.WriteLine($"TxBytes:      {nif.TxBytes}");
        //    Console.WriteLine($"TxPackets:    {nif.TxPackets}");
        //    Console.WriteLine($"TxErrors:     {nif.TxErrors}");
        //    Console.WriteLine($"TxDropped:    {nif.TxDropped}");
        //    Console.WriteLine($"TxFifo:       {nif.TxFifo}");
        //    Console.WriteLine($"TxCollisions: {nif.TxCollisions}");
        //    Console.WriteLine($"TxCarrier:    {nif.TxCarrier}");
        //    Console.WriteLine($"TxCompressed: {nif.TxCompressed}");
        //}
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

        // TODO
        //Console.WriteLine($"ProcessCount: {process.ProcessCount}");
        //Console.WriteLine($"ThreadCount:  {process.ThreadCount}");
    }

    private void SetupCpuMetric(IMetricManager manager)
    {
        var cpu = PlatformProvider.GetCpu();

        prepareEntries.Add(() => cpu.Update());

        // TODO
        //Console.WriteLine("Frequency");
        //foreach (var core in cpu.Cores)
        //{
        //    Console.WriteLine($"{core.Name}: {core.Frequency}");
        //}

        //if (cpu.Powers.Count > 0)
        //{
        //    Console.WriteLine("Power");
        //    foreach (var power in cpu.Powers)
        //    {
        //        Console.WriteLine($"{power.Name}: {power.Energy / 1000.0}");
        //    }
        //}
    }

    private void SetupBatteryMetric(IMetricManager manager)
    {
        var battery = PlatformProvider.GetBattery();
        if (!battery.Supported)
        {
            return;
        }

        prepareEntries.Add(() => battery.Update());

        var metricCapacity = manager.CreateMetric("system_battery_capacity");
        updateEntries.Add(new Entry(() => battery.Capacity, metricCapacity.CreateGauge(MakeTags())));

        var metricVoltage = manager.CreateMetric("system_battery_voltage");
        updateEntries.Add(new Entry(() => battery.Voltage / 1000.0, metricVoltage.CreateGauge(MakeTags())));

        var metricCurrent = manager.CreateMetric("system_battery_current");
        updateEntries.Add(new Entry(() => battery.Current / 1000.0, metricCurrent.CreateGauge(MakeTags())));

        var metricCharge = manager.CreateMetric("system_battery_charge");
        updateEntries.Add(new Entry(() => battery.Charge / 1000.0, metricCharge.CreateGauge(MakeTags())));

        var metricChargeFull = manager.CreateMetric("system_battery_charge_full");
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

        var metric = manager.CreateMetric("system_ac_online");
        updateEntries.Add(new Entry(() => adapter.Online ? 1 : 0, metric.CreateGauge(MakeTags())));
    }

    private void SetupHardwareMonitorMetric(IMetricManager manager)
    {
        var monitors = PlatformProvider.GetHardwareMonitors();

        //prepareEntries.Add(() => monitors.Update());

        // TODO
        //foreach (var monitor in monitors)
        //{
        //    Console.WriteLine($"Monitor: {monitor.Type}");
        //    Console.WriteLine($"Name:    {monitor.Name}");
        //    foreach (var sensor in monitor.Sensors)
        //    {
        //        Console.WriteLine($"Sensor:  {sensor.Type}");
        //        Console.WriteLine($"Label:   {sensor.Label}");
        //        Console.WriteLine($"Value:   {sensor.Value}");
        //    }
        //}
    }

    //--------------------------------------------------------------------------------
    // Entry
    //--------------------------------------------------------------------------------

    // TODO
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
