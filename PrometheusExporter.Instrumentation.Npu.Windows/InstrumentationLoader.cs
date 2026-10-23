namespace PrometheusExporter.Instrumentation.Npu.Windows;

using PrometheusExporter.Abstractions;

internal sealed class InstrumentationLoader : IInstrumentationLoader
{
    public void Load(IInstrumentationRegistry registry)
    {
        registry.Register("Npu", builder =>
        {
            builder.AddSetting<NpuOptions>();
            builder.AddInstrumentation<NpuInstrumentation>();
        });
    }
}
