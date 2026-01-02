namespace PrometheusExporter.Instrumentation.HardwareMonitor;

using PrometheusExporter.Abstractions;

internal sealed class InstrumentationLoader : IInstrumentationLoader
{
    public void Load(IInstrumentationRegistry registry)
    {
        registry.Register("HardwareMonitor", builder =>
        {
            builder.AddSetting<HardwareMonitorOptions>();
            builder.AddInstrumentation<HardwareMonitorInstrumentation>();
        });
    }
}
