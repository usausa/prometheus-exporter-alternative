namespace PrometheusExporter.Instrumentation.PerformanceCounter;

using Microsoft.Extensions.DependencyInjection;

using PrometheusExporter.Abstractions;

public static class BuilderExtensions
{
    internal static IInstrumentationBuilder AddPerformanceCounterInstrumentation(this IInstrumentationBuilder builder, PerformanceCounterOptions options)
    {
        return builder.AddInstrumentation("PerformanceCounter", p => new PerformanceCounterInstrumentation(p.GetRequiredService<IMetricManager>(), options));
    }
}
