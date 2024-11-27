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
                IGauge Factory(string id) => nvmeMetric.CreateGauge(MakeTags(options.Host, disk.Index, disk.Model, drive, id));
                nvmeDisks.Add(new NvmeDisk(Factory, (ISmartNvme)disk.Smart));
            }
            if (disk.SmartType == SmartType.Generic)
            {
                IGauge Factory(string id) => genericMetric.CreateGauge(MakeTags(options.Host, disk.Index, disk.Model, drive, id));
                genericDisks.Add(new GenericDisk(Factory, (ISmartGeneric)disk.Smart));
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

    //--------------------------------------------------------------------------------
    // Disk
    //--------------------------------------------------------------------------------

    private sealed class NvmeDisk
    {
        private readonly Func<string, IGauge> gaugeFactory;

        private readonly ISmartNvme smart;

        // TODO Array

        public NvmeDisk(Func<string, IGauge> gaugeFactory, ISmartNvme smart)
        {
            this.gaugeFactory = gaugeFactory;
            this.smart = smart;
            // TODO
        }

        public void Update()
        {
            if (smart.Update())
            {
                // TODO
            }
            else
            {
                // TODO
            }
        }
    }

    private sealed class GenericDisk
    {
        private readonly Func<string, IGauge> gaugeFactory;

        private readonly ISmartGeneric smart;

        // TODO Array

        public GenericDisk(Func<string, IGauge> gaugeFactory, ISmartGeneric smart)
        {
            this.gaugeFactory = gaugeFactory;
            this.smart = smart;
            // TODO
        }

        public void Update()
        {
            if (smart.Update())
            {
                // TODO
            }
            else
            {
                // TODO
            }
        }
    }
}
