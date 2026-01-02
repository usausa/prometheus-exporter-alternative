namespace PrometheusExporter.Instrumentation.Ping;

using PrometheusExporter.Abstractions;

public sealed class InstrumentationLoader : IInstrumentationLoader
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
