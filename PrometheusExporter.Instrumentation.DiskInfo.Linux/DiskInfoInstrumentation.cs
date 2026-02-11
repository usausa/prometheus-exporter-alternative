namespace PrometheusExporter.Instrumentation.DiskInfo;

using LinuxDotNet.Disk;

using PrometheusExporter.Abstractions;

internal sealed class DiskInfoInstrumentation : IDisposable
{
    private readonly TimeSpan updateDuration;

    private readonly IReadOnlyList<IDiskInfo> disks;

    private readonly List<NvmeDisk> nvmeDisks = [];

    private readonly List<GenericDisk> genericDisks = [];

    private DateTime lastUpdate;

    public DiskInfoInstrumentation(
        DiskInfoOptions options,
        IInstrumentationEnvironment environment,
        IMetricManager manager)
    {
        updateDuration = TimeSpan.FromMilliseconds(options.UpdateDuration);

        var sectorMetric = manager.CreateGauge("smart_disk_byte_per_sector");
        var nvmeMetric = manager.CreateGauge("smart_nvme_value");
        var genericMetric = manager.CreateGauge("smart_generic_value");

        disks = DiskInfo.GetInformation();

        foreach (var disk in disks)
        {
            var device = Path.GetFileName(disk.DeviceName);
            var sector = sectorMetric.Create(MakeTags(environment.Host, disk.Index, disk.Model, device));
            sector.Value = disk.LogicalBlockSize;

            if (disk.SmartType == SmartType.Nvme)
            {
                var smart = (ISmartNvme)disk.Smart;
                nvmeDisks.Add(new NvmeDisk(MakeNvmeMetrics(nvmeMetric, smart, environment.Host, disk, device), smart));
            }
            else if (disk.SmartType == SmartType.Generic)
            {
                var smart = (ISmartGeneric)disk.Smart;
                genericDisks.Add(new GenericDisk(MakeGenericMetrics(genericMetric, smart, environment.Host, disk, device), smart));
            }
        }

        manager.AddBeforeCollectCallback(Update);
    }

    public void Dispose()
    {
        foreach (var disk in disks)
        {
            disk.Dispose();
        }
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

        foreach (var disk in nvmeDisks)
        {
            disk.Update();
        }

        foreach (var disk in genericDisks)
        {
            disk.Update();
        }

        lastUpdate = now;
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private static KeyValuePair<string, object?>[] MakeTags(string host, uint index, string model, string device) =>
        [new("host", host), new("index", index), new("model", model), new("device", device)];

    private static KeyValuePair<string, object?>[] MakeTags(string host, uint index, string model, string device, string id) =>
        [new("host", host), new("index", index), new("model", model), new("device", device), new("smart_id", id)];

    private static IMetricSeries[] MakeNvmeMetrics(IMetric metric, ISmartNvme smart, string host, IDiskInfo disk, string device)
    {
        IMetricSeries Factory(string id) => metric.Create(MakeTags(host, disk.Index, disk.Model, device, id));

        var entries = new IMetricSeries[17 + smart.TemperatureSensors.Length];
        entries[0] = Factory("available_spare");
        entries[1] = Factory("available_spare_threshold");
        entries[2] = Factory("controller_busy_time");
        entries[3] = Factory("critical_composite_temperature_time");
        entries[4] = Factory("critical_warning");
        entries[5] = Factory("data_unit_read");
        entries[6] = Factory("data_unit_written");
        entries[7] = Factory("error_info_log_entries");
        entries[8] = Factory("host_read_commands");
        entries[9] = Factory("host_write_commands");
        entries[10] = Factory("media_errors");
        entries[11] = Factory("percentage_used");
        entries[12] = Factory("power_cycles");
        entries[13] = Factory("power_on_hours");
        entries[14] = Factory("temperature");
        entries[15] = Factory("unsafe_shutdowns");
        entries[16] = Factory("warning_composite_temperature_time");
        for (var i = 0; i < smart.TemperatureSensors.Length; i++)
        {
            entries[17 + i] = Factory($"temperature_sensor{i}");
        }

        return entries;
    }

    private static IMetricSeries?[] MakeGenericMetrics(IMetric metric, ISmartGeneric smart, string host, IDiskInfo disk, string device)
    {
        IMetricSeries Factory(string id) => metric.Create(MakeTags(host, disk.Index, disk.Model, device, id));

        var entries = new IMetricSeries?[256];
        foreach (var smartId in smart.GetSupportedIds())
        {
            var id = (byte)smartId;
            entries[id] = Factory($"{id:X2}");
        }

        return entries;
    }

    //--------------------------------------------------------------------------------
    // Disk
    //--------------------------------------------------------------------------------

    private sealed class NvmeDisk
    {
        private readonly IMetricSeries[] entries;

        private readonly ISmartNvme smart;

        public NvmeDisk(IMetricSeries[] entries, ISmartNvme smart)
        {
            this.entries = entries;
            this.smart = smart;
        }

        public void Update()
        {
            if (smart.Update())
            {
                entries[0].Value = smart.AvailableSpare;
                entries[1].Value = smart.AvailableSpareThreshold;
                entries[2].Value = smart.ControllerBusyTime;
                entries[3].Value = smart.CriticalCompositeTemperatureTime;
                entries[4].Value = smart.CriticalWarning;
                entries[5].Value = smart.DataUnitRead;
                entries[6].Value = smart.DataUnitWritten;
                entries[7].Value = smart.ErrorInfoLogEntries;
                entries[8].Value = smart.HostReadCommands;
                entries[9].Value = smart.HostWriteCommands;
                entries[10].Value = smart.MediaErrors;
                entries[11].Value = smart.PercentageUsed;
                entries[12].Value = smart.PowerCycles;
                entries[13].Value = smart.PowerOnHours;
                entries[14].Value = smart.Temperature;
                entries[15].Value = smart.UnsafeShutdowns;
                entries[16].Value = smart.WarningCompositeTemperatureTime;
                for (var i = 0; i < smart.TemperatureSensors.Length; i++)
                {
                    var value = smart.TemperatureSensors[i];
                    entries[17 + i].Value = value >= 0 ? value : double.NaN;
                }
            }
            else
            {
                foreach (var gauge in entries)
                {
                    gauge.Value = double.NaN;
                }
            }
        }
    }

    private sealed class GenericDisk
    {
        private readonly IMetricSeries?[] entries;

        private readonly ISmartGeneric smart;

        public GenericDisk(IMetricSeries?[] entries, ISmartGeneric smart)
        {
            this.entries = entries;
            this.smart = smart;
        }

        public void Update()
        {
            if (smart.Update())
            {
                foreach (var id in smart.GetSupportedIds())
                {
                    var gauge = entries[(byte)id];
                    if (gauge is not null)
                    {
                        var attr = smart.GetAttribute(id);
                        gauge.Value = attr?.RawValue ?? double.NaN;
                    }
                }
            }
            else
            {
                foreach (var gauge in entries)
                {
                    gauge?.Value = double.NaN;
                }
            }
        }
    }
}
