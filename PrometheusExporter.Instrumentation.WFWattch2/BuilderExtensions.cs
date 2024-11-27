namespace PrometheusExporter.Instrumentation.WFWattch2;

using Microsoft.Extensions.DependencyInjection;

using PrometheusExporter.Abstractions;

public static class BuilderExtensions
{
    internal static IInstrumentationBuilder AddWFWattch2Instrumentation(this IInstrumentationBuilder builder, WFWattch2Options options)
    {
        return builder.AddInstrumentation("WFWattch2", p => new WFWattch2Instrumentation(p.GetRequiredService<IMetricManager>(), options));
    }
}
