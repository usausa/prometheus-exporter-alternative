namespace PrometheusExporter.Instrumentation;

using PrometheusExporter.Abstractions;
using PrometheusExporter.Metrics;

public static class BuilderExtensions
{
    internal static IInstrumentationBuilder AddApplicationInstrumentation(this IInstrumentationBuilder builder, ApplicationOptions options)
    {
        return builder.AddInstrumentation("Application", p => new ApplicationInstrumentation(p.GetRequiredService<IMetricManager>(), p.GetRequiredService<IInstrumentationProvider>(), options));
    }
}
