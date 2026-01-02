namespace PrometheusExporter.Instrumentation.WFWattch2;

using PrometheusExporter.Abstractions;

internal sealed class InstrumentationLoader : IInstrumentationLoader
{
    public void Load(IInstrumentationRegistry registry)
    {
        registry.Register("WFWattch2", builder =>
        {
            builder.AddSetting<WFWattch2Options>();
            builder.AddInstrumentation<WFWattch2Instrumentation>();
        });
    }
}
