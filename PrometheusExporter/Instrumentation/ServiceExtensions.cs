namespace PrometheusExporter.Instrumentation;

using PrometheusExporter.Instrumentation.Application;

internal static class ServiceExtensions
{
    public static IServiceCollection AddInstrumentation(this IServiceCollection services, IConfiguration configuration, Action<LoadConfig> configure)
    {
        var config = new LoadConfig();
        configure(config);

        // Provider
        services.AddSingleton<IInstrumentationProvider, InstrumentationProvider>();

        // Environment
        var environment = new InstrumentationEnvironment(config.Host ?? Environment.MachineName);
        services.AddSingleton(environment);

        // Registration
        var registrationManager = new RegistrationManager();
        services.AddSingleton<IRegistrationManager>(registrationManager);

        // Load instrumentation
        var registry = new InstrumentationRegistry(services, configuration.GetSection("Exporter").GetSection("Instrumentation"), registrationManager);

        // Application
        registry.Register("Application", builder =>
        {
            builder.AddSetting<ApplicationOptions>();
            builder.AddInstrumentation<ApplicationInstrumentation>();
        });

        // Enable instrumentation
        // TODO
        //foreach (var name in config.Enable)
        //{
        //}

        return services;
    }
}
