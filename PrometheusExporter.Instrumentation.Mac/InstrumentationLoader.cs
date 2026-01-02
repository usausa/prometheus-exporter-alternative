namespace PrometheusExporter.Instrumentation.Mac;

using PrometheusExporter.Abstractions;

internal sealed class InstrumentationLoader : IInstrumentationLoader
{
    public void Load(IInstrumentationRegistry registry)
    {
        registry.Register("Mac", builder =>
        {
            builder.AddSetting<MacOptions>();
            builder.AddInstrumentation<MacInstrumentation>();
        });
    }
}
