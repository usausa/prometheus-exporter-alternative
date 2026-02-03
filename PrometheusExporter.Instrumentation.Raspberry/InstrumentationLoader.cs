namespace PrometheusExporter.Instrumentation.Raspberry;

using PrometheusExporter.Abstractions;

internal sealed class InstrumentationLoader : IInstrumentationLoader
{
    public void Load(IInstrumentationRegistry registry)
    {
        registry.Register("Raspberry", builder =>
        {
            builder.AddSetting<RaspberryOptions>();
            builder.AddInstrumentation<RaspberryInstrumentation>();
        });
    }
}
