namespace PrometheusExporter.Instrumentation.DiskInfo;

using PrometheusExporter.Abstractions;

internal sealed class InstrumentationLoader : IInstrumentationLoader
{
    public void Load(IInstrumentationRegistry registry)
    {
        registry.Register("DiskInfo", builder =>
        {
            builder.AddSetting<DiskInfoOptions>();
            builder.AddInstrumentation<DiskInfoInstrumentation>();
        });
    }
}
