namespace PrometheusExporter.Instrumentation.ProcessFileSystem;

using Microsoft.Extensions.DependencyInjection;

using PrometheusExporter.Abstractions;

public static class BuilderExtensions
{
    public static IInstrumentationBuilder AddProcessFileSystemInstrumentation(this IInstrumentationBuilder builder, ProcessFileSystemOptions options)
    {
        return builder.AddInstrumentation("ProcessFileSystem", p => new ProcessFileSystemInstrumentation(p.GetRequiredService<IMetricManager>(), options));
    }
}
