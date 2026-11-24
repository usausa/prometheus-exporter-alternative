namespace PrometheusExporter.Instrumentation.DeviceBattery;

using PrometheusExporter.Abstractions;

internal sealed class InstrumentationLoader : IInstrumentationLoader
{
    public void Load(IInstrumentationRegistry registry)
    {
        registry.Register("DeviceBattery", builder =>
        {
            builder.AddSetting<DeviceBatteryOptions>();
            builder.AddInstrumentation<DeviceBatteryInstrumentation>();
        });
    }
}
