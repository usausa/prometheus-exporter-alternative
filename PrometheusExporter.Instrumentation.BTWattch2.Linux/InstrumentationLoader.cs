namespace PrometheusExporter.Instrumentation.BTWattch2;

using PrometheusExporter.Abstractions;

internal sealed class InstrumentationLoader : IInstrumentationLoader
{
    public void Load(IInstrumentationRegistry registry)
    {
        registry.Register("BTWattch2", builder =>
        {
            builder.AddSetting<BTWattch2Options>();
            builder.AddInstrumentation<BTWattch2Instrumentation>();
        });
    }
}
