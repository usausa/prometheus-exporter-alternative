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
        var informationMetric = manager.CreateGauge("exporter_information");
        informationMetric.Create(
            new("host", environment.Host),
            new("version", typeof(Program).Assembly.GetName().Version),
            new("platform", ResolvePlatformString()),
            new("os", RuntimeInformation.OSDescription)).Value = 1;

        // Uptime
        var uptimeMetric = manager.CreateGauge("exporter_uptime");
        var uptime = uptimeMetric.Create([new("host", environment.Host)]);
        manager.AddBeforeCollectCallback(() =>
        {
            uptime.Value = (long)(DateTime.Now - Process.GetCurrentProcess().StartTime).TotalSeconds;
        });

        // Instrumentation
        var instrumentationMetric = manager.CreateGauge("exporter_instrumentation");
        foreach (var registration in provider.Registrations)
        {
            var gauge = instrumentationMetric.Create(new("host", environment.Host), new("name", registration.Name));
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
