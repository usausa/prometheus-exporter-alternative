namespace PrometheusExporter.Instrumentation.SwitchBot;

using PrometheusExporter.Abstractions;

internal sealed class InstrumentationLoader : IInstrumentationLoader
{
    public void Load(IInstrumentationRegistry registry)
    {
        registry.Register("SwitchBot", builder =>
        {
            builder.AddSetting<SwitchBotOptions>();
            builder.AddInstrumentation<SwitchBotInstrumentation>();
        });
    }
}
