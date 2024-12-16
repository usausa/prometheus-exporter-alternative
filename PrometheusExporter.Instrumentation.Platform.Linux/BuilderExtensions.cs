namespace PrometheusExporter.Instrumentation.Platform.Linux;

using Microsoft.Extensions.DependencyInjection;

using PrometheusExporter.Abstractions;

public static class BuilderExtensions
{
    public static IInstrumentationBuilder AddPlatformLinuxInstrumentation(this IInstrumentationBuilder builder, PlatformLinuxOptions options)
    {
        return builder.AddInstrumentation("PlatformLinux", p => new PlatformLinuxInstrumentation(p.GetRequiredService<IMetricManager>(), options));
    }
}
