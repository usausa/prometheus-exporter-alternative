namespace PrometheusExporter.Instrumentation.SensorOmron;

using Microsoft.Extensions.DependencyInjection;

using PrometheusExporter.Abstractions;

public static class BuilderExtensions
{
    public static IInstrumentationBuilder AddSensorOmronInstrumentation(this IInstrumentationBuilder builder, SensorOmronOptions options)
    {
        return builder.AddInstrumentation("SensorOmron", p => new SensorOmronInstrumentation(p.GetRequiredService<IMetricManager>(), options));
    }
}
