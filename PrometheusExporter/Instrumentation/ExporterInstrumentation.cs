namespace PrometheusExporter.Instrumentation;

using System.Diagnostics;

using PrometheusExporter.Abstractions;
using PrometheusExporter.Metrics;

internal sealed class ExporterInstrumentation
{
    public ExporterInstrumentation(
        IMetricManager manager,
        IInstrumentationProvider provider,
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
        foreach (var registration in provider.Registrations)
        {
            var gauge = instrumentationMetric.CreateGauge(new("host", options.Host), new("name", registration.Name));
            gauge.Value = 1;
        }
    }
}
