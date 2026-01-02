namespace PrometheusExporter.Instrumentation.Application;

using System.Diagnostics;
using System.Runtime.InteropServices;

using PrometheusExporter.Abstractions;

internal sealed class ApplicationInstrumentation
{
    public ApplicationInstrumentation(
        IInstrumentationEnvironment environment,
        IInstrumentationProvider provider,
        IMetricManager manager)
    {
        // Information
        var informationMetric = manager.CreateMetric("exporter_information");
        informationMetric.CreateGauge(
            new("host", environment.Host),
            new("version", typeof(Program).Assembly.GetName().Version),
            new("platform", ResolvePlatformString()),
            new("os", RuntimeInformation.OSDescription)).Value = 1;

        // Uptime
        var uptimeMetric = manager.CreateMetric("exporter_uptime");
        var uptime = uptimeMetric.CreateGauge([new("host", environment.Host)]);
        manager.AddBeforeCollectCallback(() =>
        {
            uptime.Value = (long)(DateTime.Now - Process.GetCurrentProcess().StartTime).TotalSeconds;
        });

        // Instrumentation
        var instrumentationMetric = manager.CreateMetric("exporter_instrumentation");
        foreach (var registration in provider.Registrations)
        {
            var gauge = instrumentationMetric.CreateGauge(new("host", environment.Host), new("name", registration.Name));
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
