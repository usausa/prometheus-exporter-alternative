namespace PrometheusExporter.Metrics;

using PrometheusExporter.Abstractions;

internal static class ServiceExtensions
{
    public static IServiceCollection AddMetricManager(this IServiceCollection services)
    {
        services.AddSingleton<IMetricManager, MetricManager>();

        return services;
    }
}
