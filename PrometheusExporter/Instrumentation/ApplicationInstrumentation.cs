namespace PrometheusExporter.Instrumentation;

using System.Diagnostics;
using System.Runtime.InteropServices;

using PrometheusExporter.Abstractions;
using PrometheusExporter.Metrics;

internal sealed class ApplicationInstrumentation
{
    public ApplicationInstrumentation(
        IMetricManager manager,
        IInstrumentationProvider provider,
        ApplicationOptions options)
    {
        // Information
        var informationMetric = manager.CreateMetric("exporter_information");
        informationMetric.CreateGauge(
        [
            new("host", options.Host),
            new("version", typeof(Program).Assembly.GetName().Version),
            new("platform", ResolvePlatformString()),
            new("os", RuntimeInformation.OSDescription)
        ]).Value = 1;

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

    private static string ResolvePlatformString()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "windows";
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "linux";
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "osx";
        }
        return "unknown";
    }
}
