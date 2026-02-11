namespace PrometheusExporter.Instrumentation.Mac;

using PrometheusExporter.Abstractions;

using MacDotNet.SystemInfo;

internal sealed class MacInstrumentation
{
    private readonly string host;

    private readonly TimeSpan updateDuration;

    private readonly List<Action> entries = [];

    private DateTime lastUpdate;

    public MacInstrumentation(
        MacOptions options,
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

        foreach (var action in entries)
        {
            action();
        }

        lastUpdate = now;
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private KeyValuePair<string, object?>[] MakeTags() => [new("host", host)];

    private static Action MakeEntry(Func<double> measurement, IMetricSeries series)
    {
        return () => series.Value = measurement();
    }

    //--------------------------------------------------------------------------------
    // Uptime
    //--------------------------------------------------------------------------------

    private void SetupUptimeMetric(IMetricManager manager)
    {
        // Uptime
        var uptimeInfo = PlatformProvider.GetUptime();

        var metric = manager.CreateGauge("system_uptime");
        entries.Add(MakeEntry(ReadValue, metric.Create(MakeTags())));
        return;

        double ReadValue()
        {
            uptimeInfo.Update();
            var uptime = uptimeInfo.Uptime;
            return uptime.TotalSeconds;
        }
    }
}
