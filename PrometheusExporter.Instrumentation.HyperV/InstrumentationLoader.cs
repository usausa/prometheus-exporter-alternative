namespace PrometheusExporter.Instrumentation.HyperV;

using PrometheusExporter.Abstractions;

internal sealed class InstrumentationLoader : IInstrumentationLoader
{
    public void Load(IInstrumentationRegistry registry)
    {
        registry.Register("HyperV", builder =>
        {
            builder.AddSetting<HyperVOptions>();
            builder.AddInstrumentation<HyperVInstrumentation>();
        });
    }
}
