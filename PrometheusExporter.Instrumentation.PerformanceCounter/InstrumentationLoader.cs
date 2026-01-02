namespace PrometheusExporter.Instrumentation.PerformanceCounter;

using PrometheusExporter.Abstractions;

internal sealed class InstrumentationLoader : IInstrumentationLoader
{
    public void Load(IInstrumentationRegistry registry)
    {
        registry.Register("PerformanceCounter", builder =>
        {
            builder.AddSetting<PerformanceCounterOptions>();
            builder.AddInstrumentation<PerformanceCounterInstrumentation>();
        });
    }
}
