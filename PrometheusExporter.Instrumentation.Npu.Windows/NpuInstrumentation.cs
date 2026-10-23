namespace PrometheusExporter.Instrumentation.Npu.Windows;

using System.Globalization;

using PrometheusExporter.Abstractions;
using PrometheusExporter.Infrastructure.Windows.Pdh;

internal sealed class NpuInstrumentation : IDisposable
{
    private const double LoadLimit = 100.0;

    private const double ActiveThreshold = 0.005;

    private readonly List<DeviceEntry> entries = [];

    private readonly PdhQuery? query;

    private readonly Timer? timer;

    private volatile int isRunning;

    public NpuInstrumentation(
        NpuOptions options,
        IInstrumentationEnvironment environment,
        IMetricManager manager)
    {
        var devices = ResolveDevices(options);
        if (devices.Count == 0)
        {
            return;
        }

        query = PdhQuery.Create();
        if (query is null)
        {
            return;
        }

        var counters = new List<(NpuDevice Device, PdhCounterArray Counter)>();
        foreach (var device in devices)
        {
            var path = string.Create(CultureInfo.InvariantCulture, $@"\GPU Engine(*luid_{device.Luid}*)\Utilization Percentage");
            var counter = query.AddCounterArray(path);
            if (counter is not null)
            {
                counters.Add((device, counter));
            }
        }

        // No GPU Engine counter set, so nothing to publish on this machine
        if (counters.Count == 0)
        {
            query.Dispose();
            query = null;
            return;
        }

        var informationMetric = manager.CreateGauge("hardware_npu_information");
        var loadMetric = manager.CreateGauge("hardware_npu_load");
        var loadAverageMetric = manager.CreateGauge("hardware_npu_load_average");
        var contextCountMetric = manager.CreateGauge("hardware_npu_context_count");
        var contextTimeMetric = options.ContextTime ? manager.CreateGauge("hardware_npu_context_time") : null;

        foreach (var (device, counter) in counters)
        {
            var tags = MakeTags(environment.Host, device);

            informationMetric.Create(tags).Value = 1;

            entries.Add(new DeviceEntry(
                counter,
                loadMetric.Create(tags),
                loadAverageMetric.Create(tags),
                contextCountMetric.Create(tags),
                contextTimeMetric?.Create(tags)));
        }

        timer = new Timer(Update, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(options.Interval));

        manager.AddBeforeCollectCallback(Publish);
    }

    public void Dispose()
    {
        timer?.Dispose();
        query?.Dispose();
    }

    //--------------------------------------------------------------------------------
    // Event
    //--------------------------------------------------------------------------------

    private void Update(object? state)
    {
        if (Interlocked.CompareExchange(ref isRunning, 1, 0) != 0)
        {
            return;
        }

        try
        {
            if ((query is null) || !query.Collect() || !query.IsPrimed)
            {
                return;
            }

            foreach (var entry in entries)
            {
                entry.Sample();
            }
        }
        finally
        {
            Interlocked.Exchange(ref isRunning, 0);
        }
    }

    private void Publish()
    {
        foreach (var entry in entries)
        {
            entry.Publish();
        }
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private static List<NpuDevice> ResolveDevices(NpuOptions options) =>
        string.IsNullOrEmpty(options.Luid)
            ? NpuDeviceLocator.Locate()
            : [new NpuDevice(options.Luid, 0, options.Luid)];

    private static KeyValuePair<string, object?>[] MakeTags(string host, NpuDevice device) =>
        [new("host", host), new("name", device.Description), new("luid", device.Luid)];

    //--------------------------------------------------------------------------------
    // DeviceEntry
    //--------------------------------------------------------------------------------

    private sealed class DeviceEntry
    {
        private readonly Lock sync = new();

        private readonly PdhCounterArray counter;

        private readonly IMetricSeries load;

        private readonly IMetricSeries loadAverage;

        private readonly IMetricSeries contextCount;

        private readonly IMetricSeries? contextTime;

        private double peak;

        private double total;

        private int samples;

        private double peakSum;

        private int peakActive;

        public DeviceEntry(
            PdhCounterArray counter,
            IMetricSeries load,
            IMetricSeries loadAverage,
            IMetricSeries contextCount,
            IMetricSeries? contextTime)
        {
            this.counter = counter;
            this.load = load;
            this.loadAverage = loadAverage;
            this.contextCount = contextCount;
            this.contextTime = contextTime;

            load.Value = double.NaN;
            loadAverage.Value = double.NaN;
            contextCount.Value = double.NaN;
            contextTime?.Value = double.NaN;
        }

        public void Sample()
        {
            var max = 0.0;
            var sum = 0.0;
            var active = 0;

            for (var i = 0; i < counter.Count; i++)
            {
                if (!counter.HasValue(i))
                {
                    continue;
                }

                var value = counter.GetValue(i);
                if (!double.IsFinite(value) || (value <= 0))
                {
                    continue;
                }

                if (value > LoadLimit)
                {
                    value = LoadLimit;
                }

                sum += value;
                if (value > max)
                {
                    max = value;
                }

                if (value > ActiveThreshold)
                {
                    active++;
                }
            }

            lock (sync)
            {
                if (max > peak)
                {
                    peak = max;
                }

                total += max;
                samples++;

                if (sum > peakSum)
                {
                    peakSum = sum;
                }

                if (active > peakActive)
                {
                    peakActive = active;
                }
            }
        }

        public void Publish()
        {
            double currentPeak;
            double currentAverage;
            double currentSum;
            int currentActive;

            lock (sync)
            {
                if (samples == 0)
                {
                    return;
                }

                currentPeak = peak;
                currentAverage = total / samples;
                currentSum = peakSum;
                currentActive = peakActive;

                peak = 0;
                total = 0;
                samples = 0;
                peakSum = 0;
                peakActive = 0;
            }

            load.Value = currentPeak;
            loadAverage.Value = currentAverage;
            contextCount.Value = currentActive;
            contextTime?.Value = currentSum;
        }
    }
}
