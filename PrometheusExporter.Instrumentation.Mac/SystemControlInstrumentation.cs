namespace PrometheusExporter.Instrumentation.SystemControl;

using System.Runtime.InteropServices;

using PrometheusExporter.Abstractions;

using static PrometheusExporter.Instrumentation.SystemControl.NativeMethods;

internal sealed class SystemControlInstrumentation
{
    private readonly string host;

    private readonly TimeSpan updateDuration;

    private readonly List<Entry> entries = [];

    private DateTime lastUpdate;

    public SystemControlInstrumentation(IMetricManager manager, SystemControlOptions options)
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

    //--------------------------------------------------------------------------------
    // Uptime
    //--------------------------------------------------------------------------------

    private void SetupUptimeMetric(IMetricManager manager)
    {
        var metric = manager.CreateMetric("system_uptime");
        entries.Add(new Entry(ReadValue, metric.CreateGauge(MakeTags())));

        static double ReadValue()
        {
            var time = new timeval { tv_sec = 0, tv_usec = 0 };
            var size = Marshal.SizeOf<timeval>();
            if (sysctlbyname("kern.boottime", ref time, ref size, IntPtr.Zero, 0) != 0)
            {
                return double.NaN;
            }

            var boot = DateTimeOffset.FromUnixTimeMilliseconds((time.tv_sec * 1000) + (time.tv_usec / 1000));
            var uptime = DateTimeOffset.Now - boot;
            return uptime.TotalSeconds;
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
