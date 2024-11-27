namespace PrometheusExporter.Instrumentation.Wifi;

using Microsoft.Extensions.DependencyInjection;

using PrometheusExporter.Abstractions;

public static class BuilderExtensions
{
    public static IInstrumentationBuilder AddWifiInstrumentation(this IInstrumentationBuilder builder, WifiOptions options)
    {
        return builder.AddInstrumentation("Wifi", p => new WifiInstrumentation(p.GetRequiredService<IMetricManager>(), options));
    }
}
