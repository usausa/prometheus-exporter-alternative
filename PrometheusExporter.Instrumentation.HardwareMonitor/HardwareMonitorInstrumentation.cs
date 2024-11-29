namespace PrometheusExporter.Instrumentation.HardwareMonitor;

using System.Runtime.CompilerServices;

using LibreHardwareMonitor.Hardware;

using PrometheusExporter.Abstractions;

internal sealed class HardwareMonitorInstrumentation : IDisposable
{
    private readonly string host;

    private readonly TimeSpan updateDuration;

    private readonly Computer computer;

    private readonly UpdateVisitor updateVisitor = new();

    private readonly List<Entry> entries = [];

    private DateTime lastUpdate;

    public HardwareMonitorInstrumentation(IMetricManager manager, HardwareMonitorOptions options)
    {
        host = options.Host;
        updateDuration = TimeSpan.FromMilliseconds(options.UpdateDuration);

        computer = new Computer
        {
            IsBatteryEnabled = options.IsBatteryEnabled,
            IsControllerEnabled = options.IsControllerEnabled,
            IsCpuEnabled = options.IsCpuEnabled,
            IsGpuEnabled = options.IsGpuEnabled,
            IsMemoryEnabled = options.IsMemoryEnabled,
            IsMotherboardEnabled = options.IsMotherboardEnabled,
            IsNetworkEnabled = options.IsNetworkEnabled,
            IsStorageEnabled = options.IsStorageEnabled
        };
        computer.Open();
        computer.Accept(updateVisitor);

        SetupInformationMetric(manager);
        SetupCpuMetric(manager);
        SetupGpuMetric(manager);
        SetupMemoryMetric(manager);
        SetupIoMetric(manager);
        SetupBatteryMetric(manager);
        SetupStorageMetric(manager);
        SetupNetworkMetric(manager);

        manager.AddBeforeCollectCallback(Update);
    }

    public void Dispose()
    {
        computer.Close();
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

        computer.Accept(updateVisitor);
        foreach (var entry in entries)
        {
            entry.Update();
        }

        lastUpdate = now;
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private IEnumerable<IHardware> EnumerateHardware(HardwareType type) =>
        computer.Hardware.SelectMany(EnumerateHardware).Where(x => x.HardwareType == type);

    private IEnumerable<IHardware> EnumerateHardware(params HardwareType[] types) =>
        computer.Hardware.SelectMany(EnumerateHardware).Where(x => Array.IndexOf(types, x) >= 0);

    private static IEnumerable<IHardware> EnumerateHardware(IHardware hardware)
    {
        yield return hardware;

        foreach (var subHardware in hardware.SubHardware)
        {
            foreach (var subSubHardware in EnumerateHardware(subHardware))
            {
                yield return subSubHardware;
            }
        }
    }

    private IEnumerable<ISensor> EnumerateSensors(HardwareType hardwareType, SensorType sensorType) =>
        computer.Hardware
            .SelectMany(EnumerateSensors)
            .Where(x => (x.Hardware.HardwareType == hardwareType) &&
                        (x.SensorType == sensorType));

    private IEnumerable<ISensor> EnumerateGpuSensors(SensorType sensorType) =>
        computer.Hardware
            .SelectMany(EnumerateSensors)
            .Where(x => ((x.Hardware.HardwareType == HardwareType.GpuNvidia) ||
                         (x.Hardware.HardwareType == HardwareType.GpuAmd) ||
                         (x.Hardware.HardwareType == HardwareType.GpuIntel)) &&
                        (x.SensorType == sensorType));

    private static IEnumerable<ISensor> EnumerateSensors(IHardware hardware)
    {
        foreach (var subHardware in hardware.SubHardware)
        {
            foreach (var sensor in EnumerateSensors(subHardware))
            {
                yield return sensor;
            }
        }

        foreach (var sensor in hardware.Sensors)
        {
            yield return sensor;
        }
    }

    private KeyValuePair<string, object?>[] MakeTags(IHardware hardware, string type) =>
        [new("host", host), new("identifier", hardware.Identifier), new("name", hardware.Name), new("type", type)];

    private KeyValuePair<string, object?>[] MakeTags(ISensor sensor) =>
        [new("host", host), new("identifier", sensor.Hardware.Identifier), new("hardware", sensor.Hardware.Name), new("name", sensor.Name), new("index", sensor.Index)];

    private KeyValuePair<string, object?>[] MakeTags(ISensor sensor, string type) =>
        [new("host", host), new("identifier", sensor.Hardware.Identifier), new("hardware", sensor.Hardware.Name), new("name", sensor.Name), new("index", sensor.Index), new("type", type)];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ToValue(ISensor sensor) => sensor.Value ?? double.NaN;

    //--------------------------------------------------------------------------------
    // Information
    //--------------------------------------------------------------------------------

    private void SetupInformationMetric(IMetricManager manager)
    {
        var metric = manager.CreateMetric("hardware_information");

        SetupInformationGauge("cpu", EnumerateHardware(HardwareType.Cpu));
        SetupInformationGauge("gpu", EnumerateHardware(HardwareType.GpuNvidia, HardwareType.GpuAmd, HardwareType.GpuIntel));
        SetupInformationGauge("memory", EnumerateHardware(HardwareType.Memory));
        SetupInformationGauge("motherboard", EnumerateHardware(HardwareType.Motherboard));
        SetupInformationGauge("io", EnumerateHardware(HardwareType.SuperIO));
        SetupInformationGauge("battery", EnumerateHardware(HardwareType.Battery));
        SetupInformationGauge("storage", EnumerateHardware(HardwareType.Storage));
        SetupInformationGauge("network", EnumerateHardware(HardwareType.Network));

        return;

        void SetupInformationGauge(string type, IEnumerable<IHardware> source)
        {
            foreach (var hardware in source)
            {
                metric.CreateGauge(MakeTags(hardware, type)).Value = 1;
            }
        }
    }

    //--------------------------------------------------------------------------------
    // CPU
    //--------------------------------------------------------------------------------

    private void SetupCpuMetric(IMetricManager manager)
    {
        var loadSensors = EnumerateSensors(HardwareType.Cpu, SensorType.Load)
            .Where(static x => x.Name.StartsWith("CPU Core #", StringComparison.Ordinal) || x.Name == "CPU Total")
            .ToArray();
        var clockSensors = EnumerateSensors(HardwareType.Cpu, SensorType.Clock)
            .Where(static x => !x.Name.Contains("Effective", StringComparison.Ordinal))
            .ToArray();
        var temperatureSensors = EnumerateSensors(HardwareType.Cpu, SensorType.Temperature)
            .Where(static x => !x.Name.EndsWith("Distance to TjMax", StringComparison.Ordinal) &&
                               (x.Name != "Core Max") &&
                               (x.Name != "Core Average"))
            .ToArray();
        var voltageSensors = EnumerateSensors(HardwareType.Cpu, SensorType.Voltage).ToArray();
        var currentSensors = EnumerateSensors(HardwareType.Cpu, SensorType.Current).ToArray();
        var powerSensors = EnumerateSensors(HardwareType.Cpu, SensorType.Power).ToArray();

        // CPU load
        if (loadSensors.Length > 0)
        {
            var metrics = manager.CreateMetric("hardware_cpu_load");
            entries.AddRange(loadSensors.Select(x => new Entry(x, ToValue, metrics.CreateGauge(MakeTags(x)))));
        }

        // CPU clock
        if (clockSensors.Length > 0)
        {
            var metrics = manager.CreateMetric("hardware_cpu_clock");
            entries.AddRange(clockSensors.Select(x => new Entry(x, ToValue, metrics.CreateGauge(MakeTags(x)))));
        }

        // CPU temperature
        if (temperatureSensors.Length > 0)
        {
            var metrics = manager.CreateMetric("hardware_cpu_temperature");
            entries.AddRange(temperatureSensors.Select(x => new Entry(x, ToValue, metrics.CreateGauge(MakeTags(x)))));
        }

        // CPU voltage
        if (voltageSensors.Length > 0)
        {
            var metrics = manager.CreateMetric("hardware_cpu_voltage");
            entries.AddRange(voltageSensors.Select(x => new Entry(x, ToValue, metrics.CreateGauge(MakeTags(x)))));
        }

        // CPU current
        if (currentSensors.Length > 0)
        {
            var metrics = manager.CreateMetric("hardware_cpu_current");
            entries.AddRange(currentSensors.Select(x => new Entry(x, ToValue, metrics.CreateGauge(MakeTags(x)))));
        }

        // CPU power
        if (powerSensors.Length > 0)
        {
            var metrics = manager.CreateMetric("hardware_cpu_power");
            entries.AddRange(powerSensors.Select(x => new Entry(x, ToValue, metrics.CreateGauge(MakeTags(x)))));
        }
    }

    //--------------------------------------------------------------------------------
    // GPU
    //--------------------------------------------------------------------------------

    private void SetupGpuMetric(IMetricManager manager)
    {
        var loadSensors = EnumerateGpuSensors(SensorType.Load)
            .Where(static x => x.Name.StartsWith("GPU", StringComparison.Ordinal))
            .ToArray();
        var clockSensors = EnumerateGpuSensors(SensorType.Clock).ToArray();
        var fanSensors = EnumerateGpuSensors(SensorType.Fan).ToArray();
        var temperatureSensors = EnumerateGpuSensors(SensorType.Temperature).ToArray();
        var powerSensors = EnumerateGpuSensors(SensorType.Power).ToArray();
        var memorySensors = EnumerateGpuSensors(SensorType.SmallData)
            .Where(static x => x.Name.StartsWith("GPU Memory", StringComparison.Ordinal))
            .ToArray();
        var throughputSensors = EnumerateGpuSensors(SensorType.Throughput)
            .Where(static x => x.Name.StartsWith("GPU PCIe", StringComparison.Ordinal))
            .ToArray();

        // GPU load
        if (loadSensors.Length > 0)
        {
            var metrics = manager.CreateMetric("hardware_gpu_load");
            entries.AddRange(loadSensors.Select(x => new Entry(x, ToValue, metrics.CreateGauge(MakeTags(x)))));
        }

        // GPU clock
        if (clockSensors.Length > 0)
        {
            var metrics = manager.CreateMetric("hardware_gpu_clock");
            entries.AddRange(clockSensors.Select(x => new Entry(x, ToValue, metrics.CreateGauge(MakeTags(x)))));
        }

        // GPU fan
        if (fanSensors.Length > 0)
        {
            var metrics = manager.CreateMetric("hardware_gpu_fan");
            entries.AddRange(fanSensors.Select(x => new Entry(x, ToValue, metrics.CreateGauge(MakeTags(x)))));
        }

        // GPU temperature
        if (temperatureSensors.Length > 0)
        {
            var metrics = manager.CreateMetric("hardware_gpu_temperature");
            entries.AddRange(temperatureSensors.Select(x => new Entry(x, ToValue, metrics.CreateGauge(MakeTags(x)))));
        }

        // GPU power
        if (powerSensors.Length > 0)
        {
            var metrics = manager.CreateMetric("hardware_gpu_power");
            entries.AddRange(powerSensors.Select(x => new Entry(x, ToValue, metrics.CreateGauge(MakeTags(x)))));
        }

        // GPU memory
        if (memorySensors.Length > 0)
        {
            var metrics = manager.CreateMetric("hardware_gpu_memory");
            var free = memorySensors.First(static x => x.Name == "GPU Memory Free");
            entries.Add(new Entry(free, ToValue, metrics.CreateGauge(MakeTags(free, "free"))));
            var used = memorySensors.First(static x => x.Name == "GPU Memory Used");
            entries.Add(new Entry(used, ToValue, metrics.CreateGauge(MakeTags(used, "used"))));
            var total = memorySensors.First(static x => x.Name == "GPU Memory Total");
            entries.Add(new Entry(total, ToValue, metrics.CreateGauge(MakeTags(total, "total"))));
        }

        // GPU throughput
        if (throughputSensors.Length > 0)
        {
            var metrics = manager.CreateMetric("hardware_gpu_throughput");
            var rx = throughputSensors.First(static x => x.Name == "GPU PCIe Rx");
            entries.Add(new Entry(rx, ToValue, metrics.CreateGauge(MakeTags(rx, "rx"))));
            var tx = throughputSensors.First(static x => x.Name == "GPU PCIe Tx");
            entries.Add(new Entry(tx, ToValue, metrics.CreateGauge(MakeTags(tx, "tx"))));
        }
    }

    //--------------------------------------------------------------------------------
    // Memory
    //--------------------------------------------------------------------------------

    private void SetupMemoryMetric(IMetricManager manager)
    {
        var dataSensors = EnumerateSensors(HardwareType.Memory, SensorType.Data).ToList();
        var loadSensors = EnumerateSensors(HardwareType.Memory, SensorType.Load).ToList();

        // Memory used
        if (dataSensors.Count > 0)
        {
            var metrics = manager.CreateMetric("hardware_memory_used");
            var physical = dataSensors.First(static x => x.Name == "Memory Used");
            entries.Add(new Entry(physical, ToValue, metrics.CreateGauge(MakeTags(physical, "physical"))));
            var @virtual = dataSensors.First(static x => x.Name == "Virtual Memory Used");
            entries.Add(new Entry(@virtual, ToValue, metrics.CreateGauge(MakeTags(@virtual, "virtual"))));
        }

        // Memory available
        if (dataSensors.Count > 0)
        {
            var metrics = manager.CreateMetric("hardware_memory_available");
            var physical = dataSensors.First(static x => x.Name == "Memory Available");
            entries.Add(new Entry(physical, ToValue, metrics.CreateGauge(MakeTags(physical, "physical"))));
            var @virtual = dataSensors.First(static x => x.Name == "Virtual Memory Available");
            entries.Add(new Entry(@virtual, ToValue, metrics.CreateGauge(MakeTags(@virtual, "virtual"))));
        }

        // Memory load
        if (loadSensors.Count > 0)
        {
            var metrics = manager.CreateMetric("hardware_memory_load");
            var physical = loadSensors.First(static x => x.Name == "Memory");
            entries.Add(new Entry(physical, ToValue, metrics.CreateGauge(MakeTags(physical, "physical"))));
            var @virtual = loadSensors.First(static x => x.Name == "Virtual Memory");
            entries.Add(new Entry(@virtual, ToValue, metrics.CreateGauge(MakeTags(@virtual, "virtual"))));
        }
    }

    //--------------------------------------------------------------------------------
    // I/O
    //--------------------------------------------------------------------------------

    private void SetupIoMetric(IMetricManager manager)
    {
        var controlSensors = EnumerateSensors(HardwareType.SuperIO, SensorType.Control).ToArray();
        var fanSensors = EnumerateSensors(HardwareType.SuperIO, SensorType.Fan).ToArray();
        var temperatureSensors = EnumerateSensors(HardwareType.SuperIO, SensorType.Temperature).ToArray();
        var voltageSensors = EnumerateSensors(HardwareType.SuperIO, SensorType.Voltage).ToArray();

        // I/O control
        if (controlSensors.Length > 0)
        {
            var metrics = manager.CreateMetric("hardware_io_control");
            entries.AddRange(controlSensors.Select(x => new Entry(x, ToValue, metrics.CreateGauge(MakeTags(x)))));
        }

        // I/O fan
        if (fanSensors.Length > 0)
        {
            var metrics = manager.CreateMetric("hardware_io_fan");
            entries.AddRange(fanSensors.Select(x => new Entry(x, ToValue, metrics.CreateGauge(MakeTags(x)))));
        }

        // I/O temperature
        if (temperatureSensors.Length > 0)
        {
            var metrics = manager.CreateMetric("hardware_io_temperature");
            entries.AddRange(temperatureSensors.Select(x => new Entry(x, ToValue, metrics.CreateGauge(MakeTags(x)))));
        }

        // I/O voltage
        if (voltageSensors.Length > 0)
        {
            var metrics = manager.CreateMetric("hardware_io_voltage");
            entries.AddRange(voltageSensors.Select(x => new Entry(x, ToValue, metrics.CreateGauge(MakeTags(x)))));
        }
    }

    //--------------------------------------------------------------------------------
    // Battery
    //--------------------------------------------------------------------------------

    private void SetupBatteryMetric(IMetricManager manager)
    {
        var levelChargeSensor = EnumerateSensors(HardwareType.Battery, SensorType.Voltage).FirstOrDefault(static x => x.Name == "Charge Level");
        var levelDegradationSensor = EnumerateSensors(HardwareType.Battery, SensorType.Voltage).FirstOrDefault(static x => x.Name == "Degradation Level");
        var voltageSensor = EnumerateSensors(HardwareType.Battery, SensorType.Voltage).FirstOrDefault();
        var currentSensor = EnumerateSensors(HardwareType.Battery, SensorType.Current).FirstOrDefault();
        var energySensors = EnumerateSensors(HardwareType.Battery, SensorType.Energy).ToList();
        var powerSensor = EnumerateSensors(HardwareType.Battery, SensorType.Power).FirstOrDefault();
        var timespanSensor = EnumerateSensors(HardwareType.Battery, SensorType.TimeSpan).FirstOrDefault();

        // Battery charge
        if (levelChargeSensor is not null)
        {
            var metrics = manager.CreateMetric("hardware_battery_charge");
            entries.Add(new Entry(levelChargeSensor, ToValue, metrics.CreateGauge(MakeTags(levelChargeSensor))));
        }

        // Battery degradation
        if (levelDegradationSensor is not null)
        {
            var metrics = manager.CreateMetric("hardware_battery_degradation");
            entries.Add(new Entry(levelDegradationSensor, ToValue, metrics.CreateGauge(MakeTags(levelDegradationSensor))));
        }

        // Battery voltage
        if (voltageSensor is not null)
        {
            var metrics = manager.CreateMetric("hardware_battery_voltage");
            entries.Add(new Entry(voltageSensor, ToValue, metrics.CreateGauge(MakeTags(voltageSensor))));
        }

        // Battery current
        if (currentSensor is not null)
        {
            var metrics = manager.CreateMetric("hardware_battery_current");
            entries.Add(new Entry(currentSensor, ToValue, metrics.CreateGauge(MakeTags(currentSensor))));
        }

        // Battery capacity
        if (energySensors.Count > 0)
        {
            var metrics = manager.CreateMetric("hardware_memory_capacity");
            var designed = energySensors.First(static x => x.Name == "Designed Capacity");
            entries.Add(new Entry(designed, ToValue, metrics.CreateGauge(MakeTags(designed, "designed"))));
            var full = energySensors.First(static x => x.Name == "Full Charged Capacity");
            entries.Add(new Entry(full, ToValue, metrics.CreateGauge(MakeTags(full, "full"))));
            var remaining = energySensors.First(static x => x.Name == "Remaining Capacity");
            entries.Add(new Entry(remaining, ToValue, metrics.CreateGauge(MakeTags(remaining, "remaining"))));
        }

        // Battery rate
        if (powerSensor is not null)
        {
            var metrics = manager.CreateMetric("hardware_battery_rate");
            entries.Add(new Entry(powerSensor, ToValue, metrics.CreateGauge(MakeTags(powerSensor))));
        }

        // Battery remaining
        if (timespanSensor is not null)
        {
            var metrics = manager.CreateMetric("hardware_battery_remaining");
            entries.Add(new Entry(timespanSensor, ToValue, metrics.CreateGauge(MakeTags(timespanSensor))));
        }
    }

    //--------------------------------------------------------------------------------
    // Storage
    //--------------------------------------------------------------------------------

    private void SetupStorageMetric(IMetricManager manager)
    {
        var loadSensors = EnumerateSensors(HardwareType.Storage, SensorType.Load).ToList();
        var dataSensors = EnumerateSensors(HardwareType.Storage, SensorType.Data).ToList();
        var throughputSensors = EnumerateSensors(HardwareType.Storage, SensorType.Throughput).ToList();
        var temperatureSensors = EnumerateSensors(HardwareType.Storage, SensorType.Temperature).ToArray();
        var levelSensors = EnumerateSensors(HardwareType.Storage, SensorType.Level).ToList();
        var factorSensors = EnumerateSensors(HardwareType.Storage, SensorType.Factor).ToList();

        // Storage used
        var loadUsedSensors = loadSensors.Where(static x => x.Name == "Used Space").ToArray();
        if (loadUsedSensors.Length > 0)
        {
            var metrics = manager.CreateMetric("hardware_storage_used");
            entries.AddRange(loadUsedSensors.Select(x => new Entry(x, ToValue, metrics.CreateGauge(MakeTags(x)))));
        }

        // Storage bytes
        var dataReadSensors = dataSensors.Where(static x => x.Name == "Data Read").ToArray();
        var dataWriteSensors = dataSensors.Where(static x => x.Name == "Data Written").ToArray();
        if ((dataReadSensors.Length > 0) || (dataWriteSensors.Length > 0))
        {
            var metrics = manager.CreateMetric("hardware_storage_bytes");
            entries.AddRange(dataReadSensors.Select(x => new Entry(x, ToValue, metrics.CreateGauge(MakeTags(x, "read")))));
            entries.AddRange(dataWriteSensors.Select(x => new Entry(x, ToValue, metrics.CreateGauge(MakeTags(x, "write")))));
        }

        // Storage speed
        if (throughputSensors.Count > 0)
        {
            var metrics = manager.CreateMetric("hardware_storage_speed");
            entries.AddRange(throughputSensors.Where(static x => x.Name == "Read Rate").Select(x => new Entry(x, ToValue, metrics.CreateGauge(MakeTags(x, "read")))));
            entries.AddRange(throughputSensors.Where(static x => x.Name == "Write Rate").Select(x => new Entry(x, ToValue, metrics.CreateGauge(MakeTags(x, "write")))));
        }

        // Storage temperature
        if (temperatureSensors.Length > 0)
        {
            var metrics = manager.CreateMetric("hardware_storage_temperature");
            entries.AddRange(temperatureSensors.Select(x => new Entry(x, ToValue, metrics.CreateGauge(MakeTags(x)))));
        }

        // Storage life
        var levelUsedSensors = levelSensors.Where(static x => x.Name == "Percentage Used").ToArray();
        var levelLifeSensors = levelSensors.Where(static x => x.Name == "Remaining Life").ToArray();
        if ((levelUsedSensors.Length > 0) || (levelLifeSensors.Length > 0))
        {
            var metrics = manager.CreateMetric("hardware_storage_life");
            entries.AddRange(levelUsedSensors.Select(x => new Entry(x, static y => 100 - ToValue(y), metrics.CreateGauge(MakeTags(x)))));
            entries.AddRange(levelLifeSensors.Select(x => new Entry(x, ToValue, metrics.CreateGauge(MakeTags(x)))));
        }

        // Storage spare
        var levelSpareSensors = levelSensors.Where(static x => x.Name == "Available Spare").ToArray();
        if (levelSpareSensors.Length > 0)
        {
            var metrics = manager.CreateMetric("hardware_storage_spare");
            entries.AddRange(levelSpareSensors.Select(x => new Entry(x, ToValue, metrics.CreateGauge(MakeTags(x)))));
        }

        // Storage amplification
        var factorAmplificationSensors = factorSensors.Where(static x => x.Name == "Write Amplification").ToArray();
        if (factorAmplificationSensors.Length > 0)
        {
            var metrics = manager.CreateMetric("hardware_storage_amplification");
            entries.AddRange(factorAmplificationSensors.Select(x => new Entry(x, ToValue, metrics.CreateGauge(MakeTags(x)))));
        }
    }

    //--------------------------------------------------------------------------------
    // Network
    //--------------------------------------------------------------------------------

    private void SetupNetworkMetric(IMetricManager manager)
    {
        var dataSensors = EnumerateSensors(HardwareType.Network, SensorType.Data).ToList();
        var throughputSensors = EnumerateSensors(HardwareType.Network, SensorType.Throughput).ToList();
        var loadSensors = EnumerateSensors(HardwareType.Network, SensorType.Load).ToArray();

        // Network bytes
        if (dataSensors.Count > 0)
        {
            var metrics = manager.CreateMetric("hardware_network_bytes");
            entries.AddRange(dataSensors.Where(static x => x.Name == "Data Downloaded").Select(x => new Entry(x, ToValue, metrics.CreateGauge(MakeTags(x, "download")))));
            entries.AddRange(dataSensors.Where(static x => x.Name == "Data Uploaded").Select(x => new Entry(x, ToValue, metrics.CreateGauge(MakeTags(x, "upload")))));
        }

        // Network speed
        if (throughputSensors.Count > 0)
        {
            var metrics = manager.CreateMetric("hardware_network_speed");
            entries.AddRange(throughputSensors.Where(static x => x.Name == "Download Speed").Select(x => new Entry(x, ToValue, metrics.CreateGauge(MakeTags(x, "download")))));
            entries.AddRange(throughputSensors.Where(static x => x.Name == "Upload Speed").Select(x => new Entry(x, ToValue, metrics.CreateGauge(MakeTags(x, "upload")))));
        }

        // Network load
        if (loadSensors.Length > 0)
        {
            var metrics = manager.CreateMetric("hardware_network_load");
            entries.AddRange(loadSensors.Select(x => new Entry(x, ToValue, metrics.CreateGauge(MakeTags(x)))));
        }
    }

    //--------------------------------------------------------------------------------
    // Entry
    //--------------------------------------------------------------------------------

    public sealed class Entry
    {
        private readonly ISensor sensor;

        private readonly Func<ISensor, double> measurement;

        private readonly IGauge gauge;

        public Entry(ISensor sensor, Func<ISensor, double> measurement, IGauge gauge)
        {
            this.sensor = sensor;
            this.measurement = measurement;
            this.gauge = gauge;
        }

        public void Update()
        {
            gauge.Value = measurement(sensor);
        }
    }
}
