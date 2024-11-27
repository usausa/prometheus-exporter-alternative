namespace PrometheusExporter.Instrumentation.HardwareMonitor;

using Microsoft.Extensions.DependencyInjection;

using PrometheusExporter.Abstractions;

public static class BuilderExtensions
{
    public static IInstrumentationBuilder AddHardwareMonitorInstrumentation(this IInstrumentationBuilder builder, HardwareMonitorOptions options)
    {
        return builder.AddInstrumentation("HardwareMonitor", p => new HardwareMonitorInstrumentation(p.GetRequiredService<IMetricManager>(), options));
    }
}
