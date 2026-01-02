namespace PrometheusExporter.Instrumentation.Wifi;

using PrometheusExporter.Abstractions;

internal sealed class InstrumentationLoader : IInstrumentationLoader
{
    public void Load(IInstrumentationRegistry registry)
    {
        registry.Register("Wifi", builder =>
        {
            builder.AddSetting<WifiOptions>();
            builder.AddInstrumentation<WifiInstrumentation>();
        });
    }
}
