namespace PrometheusExporter.Instrumentation.DiskInfo;

using HardwareInfo.Disk;

using PrometheusExporter.Abstractions;

internal sealed class DiskInfoInstrumentation : IDisposable
{
    private readonly TimeSpan updateDuration;

    private readonly IReadOnlyList<IDiskInfo> disks;

    private readonly List<NvmeDisk> nvmeDisks = [];

    private readonly List<GenericDisk> genericDisks = [];

    private DateTime lastUpdate;

    public DiskInfoInstrumentation(IMetricManager manager, DiskInfoOptions options)
    {
        updateDuration = TimeSpan.FromMilliseconds(options.UpdateDuration);

        var sectorMetric = manager.CreateMetric("smart_disk_byte_per_sector");
        var nvmeMetric = manager.CreateMetric("smart_nvme_value");
        var genericMetric = manager.CreateMetric("smart_generic_value");

        disks = DiskInfo.GetInformation();

        foreach (var disk in disks)
        {
            var drive = MakeDriveValue(disk);
            var sector = sectorMetric.CreateGauge(MakeTags(options.Host, disk.Index, disk.Model, drive));
            sector.Value = disk.BytesPerSector;

            if (disk.SmartType == SmartType.Nvme)
            {
                var smart = (ISmartNvme)disk.Smart;
                nvmeDisks.Add(new NvmeDisk(MakeNvmeGauges(nvmeMetric, smart, options.Host, disk, drive), smart));
            }
            else if (disk.SmartType == SmartType.Generic)
            {
                var smart = (ISmartGeneric)disk.Smart;
                genericDisks.Add(new GenericDisk(MakeGenericGauges(genericMetric, smart, options.Host, disk, drive), smart));
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

    private static string MakeDriveValue(IDiskInfo disk) =>
        String.Concat(disk.GetDrives().Select(static x => x.Name.TrimEnd(':')));

    private static KeyValuePair<string, object?>[] MakeTags(string host, uint index, string model, string drive) =>
        [new("host", host), new("index", index), new("model", model), new("drive", drive)];

    private static KeyValuePair<string, object?>[] MakeTags(string host, uint index, string model, string drive, string id) =>
        [new("host", host), new("index", index), new("model", model), new("drive", drive), new("smart_id", id)];

    private static IGauge[] MakeNvmeGauges(IMetric metric, ISmartNvme smart, string host, IDiskInfo disk, string drive)
    {
        IGauge Factory(string id) => metric.CreateGauge(MakeTags(host, disk.Index, disk.Model, drive, id));

        var gauges = new IGauge[17 + smart.TemperatureSensors.Length];
        gauges[0] = Factory("available_spare");
        gauges[1] = Factory("available_spare_threshold");
        gauges[2] = Factory("controller_busy_time");
        gauges[3] = Factory("critical_composite_temperature_time");
        gauges[4] = Factory("critical_warning");
        gauges[5] = Factory("data_unit_read");
        gauges[6] = Factory("data_unit_written");
        gauges[7] = Factory("error_info_log_entries");
        gauges[8] = Factory("host_read_commands");
        gauges[9] = Factory("host_write_commands");
        gauges[10] = Factory("media_errors");
        gauges[11] = Factory("percentage_used");
        gauges[12] = Factory("power_cycles");
        gauges[13] = Factory("power_on_hours");
        gauges[14] = Factory("temperature");
        gauges[15] = Factory("unsafe_shutdowns");
        gauges[16] = Factory("warning_composite_temperature_time");
        for (var i = 0; i < smart.TemperatureSensors.Length; i++)
        {
            gauges[17 + i] = Factory($"temperature_sensor{i}");
        }

        return gauges;
    }

    private static IGauge?[] MakeGenericGauges(IMetric metric, ISmartGeneric smart, string host, IDiskInfo disk, string drive)
    {
        IGauge Factory(string id) => metric.CreateGauge(MakeTags(host, disk.Index, disk.Model, drive, id));

        var gauges = new IGauge?[256];
        foreach (var smartId in smart.GetSupportedIds())
        {
            var id = (byte)smartId;
            gauges[id] = Factory($"{id:X2}");
        }

        return gauges;
    }

    //--------------------------------------------------------------------------------
    // Disk
    //--------------------------------------------------------------------------------

    private sealed class NvmeDisk
    {
        private readonly IGauge[] gauges;

        private readonly ISmartNvme smart;

        public NvmeDisk(IGauge[] gauges, ISmartNvme smart)
        {
            this.gauges = gauges;
            this.smart = smart;
        }

        public void Update()
        {
            if (smart.Update())
            {
                gauges[0].Value = smart.AvailableSpare;
                gauges[1].Value = smart.AvailableSpareThreshold;
                gauges[2].Value = smart.ControllerBusyTime;
                gauges[3].Value = smart.CriticalCompositeTemperatureTime;
                gauges[4].Value = smart.CriticalWarning;
                gauges[5].Value = smart.DataUnitRead;
                gauges[6].Value = smart.DataUnitWritten;
                gauges[7].Value = smart.ErrorInfoLogEntries;
                gauges[8].Value = smart.HostReadCommands;
                gauges[9].Value = smart.HostWriteCommands;
                gauges[10].Value = smart.MediaErrors;
                gauges[11].Value = smart.PercentageUsed;
                gauges[12].Value = smart.PowerCycles;
                gauges[13].Value = smart.PowerOnHours;
                gauges[14].Value = smart.Temperature;
                gauges[15].Value = smart.UnsafeShutdowns;
                gauges[16].Value = smart.WarningCompositeTemperatureTime;
                for (var i = 0; i < smart.TemperatureSensors.Length; i++)
                {
                    var value = smart.TemperatureSensors[i];
                    gauges[17 + i].Value = value >= 0 ? value : double.NaN;
                }
            }
            else
            {
                foreach (var gauge in gauges)
                {
                    gauge.Value = double.NaN;
                }
            }
        }
    }

    private sealed class GenericDisk
    {
        private readonly IGauge?[] gauges;

        private readonly ISmartGeneric smart;

        public GenericDisk(IGauge?[] gauges, ISmartGeneric smart)
        {
            this.gauges = gauges;
            this.smart = smart;
        }

        public void Update()
        {
            if (smart.Update())
            {
                foreach (var id in smart.GetSupportedIds())
                {
                    var gauge = gauges[(byte)id];
                    if (gauge is not null)
                    {
                        var attr = smart.GetAttribute(id);
                        gauge.Value = attr?.RawValue ?? double.NaN;
                    }
                }
            }
            else
            {
                foreach (var gauge in gauges)
                {
                    if (gauge is not null)
                    {
                        gauge.Value = double.NaN;
                    }
                }
            }
        }
    }
}
