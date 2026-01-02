namespace PrometheusExporter.Instrumentation.Application;

using PrometheusExporter.Abstractions;

internal sealed class InstrumentationLoader : IInstrumentationLoader
{
    public void Load(IInstrumentationRegistry registry)
    {
        registry.Register("Application", builder =>
        {
            builder.AddSetting<ApplicationOptions>();
            builder.AddInstrumentation<ApplicationInstrumentation>();
        });
    }
}
