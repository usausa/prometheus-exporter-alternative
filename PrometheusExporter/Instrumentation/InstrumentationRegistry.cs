namespace PrometheusExporter.Instrumentation;

using PrometheusExporter.Abstractions;

internal sealed class InstrumentationRegistry : IInstrumentationRegistry
{
    private readonly IServiceCollection services;

    private readonly IConfigurationSection configuration;

    private readonly RegistrationManager registrationManager;

    public InstrumentationRegistry(IServiceCollection services, IConfigurationSection configuration, RegistrationManager registrationManager)
    {
        this.services = services;
        this.configuration = configuration;
        this.registrationManager = registrationManager;
    }

    public void Register(string name, Action<IInstrumentationBuilder> configure)
    {
        var builder = new InstrumentationBuilder(name, services, configuration, registrationManager);

        configure(builder);
    }
}
