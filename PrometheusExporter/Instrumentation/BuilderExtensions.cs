namespace PrometheusExporter.Instrumentation;

using PrometheusExporter.Abstractions;
using PrometheusExporter.Metrics;

public static class BuilderExtensions
{
    internal static IInstrumentationBuilder AddExporterInstrumentation(this IInstrumentationBuilder builder, ExporterOptions options)
    {
        return builder.AddInstrumentation("Exporter", p => new ExporterInstrumentation(p.GetRequiredService<IMetricManager>(), p.GetRequiredService<IInstrumentationProvider>(), options));
    }
}
