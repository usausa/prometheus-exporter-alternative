namespace PrometheusExporter.Instrumentation.Linux;

using PrometheusExporter.Abstractions;

internal sealed class InstrumentationLoader : IInstrumentationLoader
{
    public void Load(IInstrumentationRegistry registry)
    {
        registry.Register("Linux", builder =>
        {
            builder.AddSetting<LinuxOptions>();
            builder.AddInstrumentation<LinuxInstrumentation>();
        });
    }
}
