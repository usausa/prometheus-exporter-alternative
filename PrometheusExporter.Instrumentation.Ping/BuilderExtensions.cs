namespace PrometheusExporter.Instrumentation.Ping;

using Microsoft.Extensions.DependencyInjection;

using PrometheusExporter.Abstractions;

public static class BuilderExtensions
{
    internal static IInstrumentationBuilder AddPingInstrumentation(this IInstrumentationBuilder builder, PingOptions options)
    {
        return builder.AddInstrumentation("Ping", p => new PingInstrumentation(p.GetRequiredService<IMetricManager>(), options));
    }
}
