namespace PrometheusExporter.Instrumentation;

using System.Reflection;
using System.Runtime.Loader;

using PrometheusExporter.Abstractions;
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
        services.AddSingleton<IRegistrationManager>(p =>
        {
            var log = p.GetRequiredService<ILogger<RegistrationManager>>();

            var registrationManager = new RegistrationManager();

            // Load instrumentation
            var registry = new InstrumentationRegistry(services, configuration.GetSection("Exporter").GetSection("Instrumentation"), registrationManager);

            // Application
            registry.Register("Application", builder =>
            {
                builder.AddInstrumentation<ApplicationInstrumentation>();
            });

            // Enable instrumentation
            foreach (var name in config.Enable)
            {
                var assemblyName = "PrometheusExporter.Instrumentation." + name;

#pragma warning disable CA1031
                try
                {
                    var dllPath = Path.Combine(AppContext.BaseDirectory, assemblyName + ".dll");
                    var assembly = File.Exists(dllPath)
                        ? Assembly.LoadFrom(dllPath)
                        : AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName(assemblyName));
                    foreach (var loaderType in assembly.GetTypes()
                                 .Where(static x => typeof(IInstrumentationLoader).IsAssignableFrom(x) && x is { IsInterface: false, IsAbstract: false }))
                    {
                        var loader = (IInstrumentationLoader)Activator.CreateInstance(loaderType)!;
                        loader.Load(registry);
                    }
                }
                catch (Exception ex)
                {
                    log.ErrorInstrumentationLoadFailed(ex, assemblyName);
                }
#pragma warning restore CA1031
            }

            return registrationManager;
        });

        return services;
    }
}
