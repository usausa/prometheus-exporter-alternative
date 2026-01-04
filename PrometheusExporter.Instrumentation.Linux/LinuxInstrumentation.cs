namespace PrometheusExporter.Instrumentation.Linux;

using System.Globalization;

using LinuxDotNet.SystemInfo;

using PrometheusExporter.Abstractions;

internal sealed class LinuxInstrumentation
{
    private readonly string host;

    private readonly TimeSpan updateDuration;

    private readonly List<Entry> entries = [];

    private DateTime lastUpdate;

    public LinuxInstrumentation(
        LinuxOptions options,
        IInstrumentationEnvironment environment,
        IMetricManager manager)
    {
        host = environment.Host;
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
        // Uptime
        var uptimeInfo = PlatformProvider.GetUptime();

        var metric = manager.CreateMetric("system_uptime");
        entries.Add(new Entry(ReadValue, metric.CreateGauge(MakeTags())));
        return;

        double ReadValue()
        {
            uptimeInfo.Update();
            var uptime = uptimeInfo.Uptime;
            return uptime.TotalSeconds;
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
