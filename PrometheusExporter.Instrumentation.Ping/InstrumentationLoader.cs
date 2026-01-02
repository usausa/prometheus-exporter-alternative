namespace PrometheusExporter.Instrumentation.Ping;

using PrometheusExporter.Abstractions;

internal sealed class InstrumentationLoader : IInstrumentationLoader
{
    public void Load(IInstrumentationRegistry registry)
    {
        registry.Register("Ping", builder =>
        {
            builder.AddSetting<PingOptions>();
            builder.AddInstrumentation<PingInstrumentation>();
        });
    }
}
