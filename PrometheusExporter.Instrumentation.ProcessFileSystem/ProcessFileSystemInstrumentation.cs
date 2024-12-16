namespace PrometheusExporter.Instrumentation.ProcessFileSystem;

using System.Globalization;

using PrometheusExporter.Abstractions;

internal sealed class ProcessFileSystemInstrumentation
{
    private readonly string host;

    private readonly TimeSpan updateDuration;

    private readonly List<Entry> entries = [];

    private DateTime lastUpdate;

    public ProcessFileSystemInstrumentation(IMetricManager manager, ProcessFileSystemOptions options)
    {
        host = options.Host;
        updateDuration = TimeSpan.FromMilliseconds(options.UpdateDuration);

        SetupUptimeMetric(manager);

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

        foreach (var entry in entries)
        {
            entry.Update();
        }

        lastUpdate = now;
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private KeyValuePair<string, object?>[] MakeTags() => [new("host", host)];

    private static string ReadString(string filename) =>
        File.ReadAllText(filename);

    //--------------------------------------------------------------------------------
    // Uptime
    //--------------------------------------------------------------------------------

    private void SetupUptimeMetric(IMetricManager manager)
    {
        var metric = manager.CreateMetric("system_uptime");
        entries.Add(new Entry(ReadUptime, metric.CreateGauge(MakeTags())));

        static double ReadUptime()
        {
            // TODO
            var str = ReadString("/proc/uptime");
            return Double.Parse(str.Split(' ')[0], CultureInfo.InvariantCulture);
        }
    }

    //--------------------------------------------------------------------------------
    // Entry
    //--------------------------------------------------------------------------------

    public sealed class Entry
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
