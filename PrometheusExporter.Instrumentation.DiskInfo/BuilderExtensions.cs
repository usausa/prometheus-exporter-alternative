namespace PrometheusExporter.Instrumentation.DiskInfo;

using Microsoft.Extensions.DependencyInjection;

using PrometheusExporter.Abstractions;

public static class BuilderExtensions
{
    public static IInstrumentationBuilder AddDiskInfoInstrumentation(this IInstrumentationBuilder builder, DiskInfoOptions options)
    {
        return builder.AddInstrumentation("DiskInfo", p => new DiskInfoInstrumentation(p.GetRequiredService<IMetricManager>(), options));
    }
}
