namespace PrometheusExporter.Instrumentation.SystemControl;

using Microsoft.Extensions.DependencyInjection;

using PrometheusExporter.Abstractions;

public static class BuilderExtensions
{
    public static IInstrumentationBuilder AddSystemControlInstrumentation(this IInstrumentationBuilder builder, SystemControlOptions options)
    {
        return builder.AddInstrumentation("SystemControl", p => new SystemControlInstrumentation(p.GetRequiredService<IMetricManager>(), options));
    }
}
