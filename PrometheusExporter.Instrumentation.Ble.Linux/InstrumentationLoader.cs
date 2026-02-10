namespace PrometheusExporter.Instrumentation.Ble;

using PrometheusExporter.Abstractions;

internal sealed class InstrumentationLoader : IInstrumentationLoader
{
    public void Load(IInstrumentationRegistry registry)
    {
        registry.Register("Ble", builder =>
        {
            builder.AddSetting<BleOptions>();
            builder.AddInstrumentation<BleInstrumentation>();
        });
    }
}
