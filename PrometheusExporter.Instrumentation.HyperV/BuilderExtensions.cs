namespace PrometheusExporter.Instrumentation.HyperV;

using Microsoft.Extensions.DependencyInjection;

using PrometheusExporter.Abstractions;

public static class BuilderExtensions
{
    public static IInstrumentationBuilder AddHyperVInstrumentation(this IInstrumentationBuilder builder, HyperVOptions options)
    {
        return builder.AddInstrumentation("HyperV", p => new HyperVInstrumentation(p.GetRequiredService<IMetricManager>(), options));
    }
}
