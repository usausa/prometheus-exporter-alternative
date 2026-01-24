namespace PrometheusExporter.Instrumentation.HyperV;

using System.Management;
using System.Text.RegularExpressions;

using PrometheusExporter.Abstractions;

internal sealed class HyperVInstrumentation
{
    private readonly string host;

    private readonly TimeSpan updateDuration;

    private readonly Regex? filter;

    private readonly IGauge countGauge;

    private readonly IMetric informationMetric;

    private readonly IMetric stateMetric;

    private readonly IMetric processorLoadMetric;

    private readonly IMetric memoryUsageMetric;

    private readonly IMetric uptimeMetric;

    private readonly List<VirtualMachine> virtualMachines = [];

    private DateTime lastUpdate;

    public HyperVInstrumentation(
        HyperVOptions options,
        IInstrumentationEnvironment environment,
        IMetricManager manager)
    {
        host = environment.Host;
        updateDuration = TimeSpan.FromMilliseconds(options.UpdateDuration);
        filter = !String.IsNullOrEmpty(options.IgnoreExpression)
            ? new Regex(options.IgnoreExpression, RegexOptions.Compiled)
            : null;

        countGauge = manager.CreateGauge("hyperv_vm_count").CreateGauge([new("host", environment.Host)]);
        informationMetric = manager.CreateGauge("hyperv_vm_information", "name");
        stateMetric = manager.CreateGauge("hyperv_vm_state", "name");
        processorLoadMetric = manager.CreateGauge("hyperv_vm_processor_load", "name");
        memoryUsageMetric = manager.CreateGauge("hyperv_vm_memory_usage", "name");
        uptimeMetric = manager.CreateGauge("hyperv_vm_uptime", "name");

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

        // Prepare update
        foreach (var vm in virtualMachines)
        {
            vm.Detected = false;
        }

        // Update
        using var searcher = new ManagementObjectSearcher(@"root\virtualization\v2", "SELECT * FROM Msvm_SummaryInformation");
        foreach (var mo in searcher.Get())
        {
            var guid = (string)mo["Name"];
            var name = (string)mo["ElementName"];
            var version = (string)mo["Version"];

            if (filter?.IsMatch(name) ?? false)
            {
                continue;
            }

            var vm = default(VirtualMachine);
            foreach (var virtualMachine in virtualMachines)
            {
                if (virtualMachine.Guid == guid)
                {
                    vm = virtualMachine;
                    break;
                }
            }

            if ((vm is not null) && ((vm.Name != name) || (vm.Version != version)))
            {
                vm = null;
            }

            if (vm is null)
            {
                var informationTags = MakeTags(guid, name, version);
                var valueTags = MakeTags(guid, name);
                vm = new VirtualMachine(
                    guid,
                    name,
                    version,
                    informationMetric.CreateGauge(informationTags),
                    stateMetric.CreateGauge(valueTags),
                    processorLoadMetric.CreateGauge(valueTags),
                    memoryUsageMetric.CreateGauge(valueTags),
                    uptimeMetric.CreateGauge(valueTags));
                virtualMachines.Add(vm);
                vm.Information.Value = 1;
            }

            var state = (ushort)mo["EnabledState"];

            vm.Detected = true;
            vm.State.Value = state;
            if (state == 2)
            {
                vm.ProcessorLoad.Value = (ushort)mo["ProcessorLoad"];
                vm.MemoryUsage.Value = (ulong)mo["MemoryUsage"];
                vm.Uptime.Value = (ulong)mo["UpTime"];
            }
            else
            {
                vm.ProcessorLoad.Value = double.NaN;
                vm.MemoryUsage.Value = double.NaN;
                vm.Uptime.Value = double.NaN;
            }
        }

        countGauge.Value = virtualMachines.Count;

        // Post update
        for (var i = virtualMachines.Count - 1; i >= 0; i--)
        {
            var vm = virtualMachines[i];
            if (!vm.Detected)
            {
                virtualMachines.RemoveAt(i);
                vm.Unregister();
            }
        }

        lastUpdate = now;
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private KeyValuePair<string, object?>[] MakeTags(string guid, string name, string version) =>
        [new("host", host), new("guid", guid), new("name", name), new("version", version)];

    private KeyValuePair<string, object?>[] MakeTags(string guid, string name) =>
        [new("host", host), new("guid", guid), new("name", name)];

    //--------------------------------------------------------------------------------
    // VirtualMachine
    //--------------------------------------------------------------------------------

    private sealed class VirtualMachine
    {
        public bool Detected { get; set; }

        public string Guid { get; }

        public string Name { get; }

        public string Version { get; }

        public IGauge Information { get; }

        public IGauge State { get; }

        public IGauge ProcessorLoad { get; }

        public IGauge MemoryUsage { get; }

        public IGauge Uptime { get; }

        public VirtualMachine(string guid, string name, string version, IGauge information, IGauge state, IGauge processorLoad, IGauge memoryUsage, IGauge uptime)
        {
            Guid = guid;
            Name = name;
            Version = version;
            Information = information;
            State = state;
            ProcessorLoad = processorLoad;
            MemoryUsage = memoryUsage;
            Uptime = uptime;
        }

        public void Unregister()
        {
            Information.Remove();
            State.Remove();
            ProcessorLoad.Remove();
            MemoryUsage.Remove();
            Uptime.Remove();
        }
    }
}
