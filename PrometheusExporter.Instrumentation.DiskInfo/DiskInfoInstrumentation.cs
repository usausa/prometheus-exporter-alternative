namespace PrometheusExporter.Instrumentation.DiskInfo;

#if BUILD_PLATFORM_WINDOWS
using HardwareInfo.Disk;
#endif
#if BUILD_PLATFORM_LINUX
using LinuxDotNet.Disk;
#endif

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
#if BUILD_PLATFORM_WINDOWS
            var name = String.Concat(disk.GetDrives().Select(static x => x.Name.TrimEnd(':')));
#endif
#if BUILD_PLATFORM_LINUX
            var name = Path.GetFileName(disk.DeviceName);
#endif

            var sector = sectorMetric.CreateGauge(MakeTags(environment.Host, disk.Index, disk.Model, name));
#if BUILD_PLATFORM_WINDOWS
            sector.Value = disk.BytesPerSector;
#endif
#if BUILD_PLATFORM_LINUX
            sector.Value = disk.LogicalBlockSize;
#endif

            if (disk.SmartType == SmartType.Nvme)
            {
                var smart = (ISmartNvme)disk.Smart;
                nvmeDisks.Add(new NvmeDisk(MakeNvmeGauges(nvmeMetric, smart, environment.Host, disk, name), smart));
            }
            else if (disk.SmartType == SmartType.Generic)
            {
                var smart = (ISmartGeneric)disk.Smart;
                genericDisks.Add(new GenericDisk(MakeGenericGauges(genericMetric, smart, environment.Host, disk, name), smart));
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

#if BUILD_PLATFORM_WINDOWS
    private static KeyValuePair<string, object?>[] MakeTags(string host, uint index, string model, string name) =>
        [new("host", host), new("index", index), new("model", model), new("drive", name)];

    private static KeyValuePair<string, object?>[] MakeTags(string host, uint index, string model, string name, string id) =>
        [new("host", host), new("index", index), new("model", model), new("drive", name), new("smart_id", id)];
#endif
#if BUILD_PLATFORM_LINUX
    private static KeyValuePair<string, object?>[] MakeTags(string host, uint index, string model, string name) =>
        [new("host", host), new("index", index), new("model", model), new("device", name)];

    private static KeyValuePair<string, object?>[] MakeTags(string host, uint index, string model, string name, string id) =>
        [new("host", host), new("index", index), new("model", model), new("device", name), new("smart_id", id)];
#endif

    private static IGauge[] MakeNvmeGauges(IMetric metric, ISmartNvme smart, string host, IDiskInfo disk, string name)
    {
        IGauge Factory(string id) => metric.CreateGauge(MakeTags(host, disk.Index, disk.Model, name, id));

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

    private static IGauge?[] MakeGenericGauges(IMetric metric, ISmartGeneric smart, string host, IDiskInfo disk, string name)
    {
        IGauge Factory(string id) => metric.CreateGauge(MakeTags(host, disk.Index, disk.Model, name, id));

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
                    gauge?.Value = double.NaN;
                }
            }
        }
    }
}
