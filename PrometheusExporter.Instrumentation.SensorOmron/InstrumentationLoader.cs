namespace PrometheusExporter.Instrumentation.SensorOmron;

using PrometheusExporter.Abstractions;

internal sealed class InstrumentationLoader : IInstrumentationLoader
{
    public void Load(IInstrumentationRegistry registry)
    {
        registry.Register("SensorOmron", builder =>
        {
            builder.AddSetting<SensorOmronOptions>();
            builder.AddInstrumentation<SensorOmronInstrumentation>();
        });
    }
}
