namespace PrometheusExporter.Instrumentation.SwitchBot;

using Microsoft.Extensions.DependencyInjection;

using PrometheusExporter.Abstractions;

public static class BuilderExtensions
{
    public static IInstrumentationBuilder AddSwitchBotInstrumentation(this IInstrumentationBuilder builder, SwitchBotOptions options)
    {
        return builder.AddInstrumentation("SwitchBot", p => new SwitchBotInstrumentation(p.GetRequiredService<IMetricManager>(), options));
    }
}
