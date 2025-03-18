namespace PrometheusExporter.Instrumentation.BTWattch2;

using Microsoft.Extensions.DependencyInjection;

using PrometheusExporter.Abstractions;

public static class BuilderExtensions
{
    public static IInstrumentationBuilder AddBTWattch2Instrumentation(this IInstrumentationBuilder builder, BTWattch2Options options)
    {
        return builder.AddInstrumentation("BTWattch2", p => new BTWattch2Instrumentation(p.GetRequiredService<IMetricManager>(), options));
    }
}
