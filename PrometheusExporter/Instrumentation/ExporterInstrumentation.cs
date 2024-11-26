namespace PrometheusExporter.Instrumentation;

using System.Diagnostics;

using PrometheusExporter.Abstractions;

internal sealed class ExporterInstrumentation
{
    public ExporterInstrumentation(
        IMetricManager manager,
        ExporterOptions options)
    {
        // Uptime
        var uptimeMetric = manager.CreateMetric("exporter_uptime");
        var uptime = uptimeMetric.CreateGauge([new("host", options.Host)]);
        manager.AddBeforeCollectCallback(() =>
        {
            uptime.Value = (long)(DateTime.Now - Process.GetCurrentProcess().StartTime).TotalSeconds;
        });

        // Instrumentation
        var instrumentationMetric = manager.CreateMetric("exporter_instrumentation");
        foreach (var instrumentation in options.InstrumentationList)
        {
            var gauge = instrumentationMetric.CreateGauge(new("host", options.Host), new("name", instrumentation));
            gauge.Value = 1;
        }
    }
}
