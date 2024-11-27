namespace PrometheusExporter.Instrumentation.Ble;

using Microsoft.Extensions.DependencyInjection;

using PrometheusExporter.Abstractions;

public static class BuilderExtensions
{
    internal static IInstrumentationBuilder AddBleInstrumentation(this IInstrumentationBuilder builder, BleOptions options)
    {
        return builder.AddInstrumentation("Ble", p => new BleInstrumentation(p.GetRequiredService<IMetricManager>(), options));
    }
}
