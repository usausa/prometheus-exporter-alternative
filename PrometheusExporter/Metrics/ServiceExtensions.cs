namespace PrometheusExporter.Metrics;

using PrometheusExporter.Abstractions;

internal static class ServiceExtensions
{
    public static IServiceCollection AddPrometheusMetrics(this IServiceCollection services, Action<IInstrumentationBuilder, IServiceCollection> configure)
    {
        var registrations = new List<Registration>();

        services.AddSingleton<IMetricManager, MetricManager>();
        services.AddSingleton<IInstrumentationProvider>(p => new InstrumentationProvider(p.GetRequiredService<ILogger<InstrumentationProvider>>(), p, registrations));

        configure(new InstrumentationBuilder(services, registrations), services);

        return services;
    }
}
