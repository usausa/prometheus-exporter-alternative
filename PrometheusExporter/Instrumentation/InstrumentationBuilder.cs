namespace PrometheusExporter.Instrumentation;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using PrometheusExporter.Abstractions;

internal sealed class InstrumentationBuilder : IInstrumentationBuilder
{
    private readonly string name;

    private readonly IServiceCollection services;

    private readonly IConfigurationSection configuration;

    private readonly RegistrationManager registrationManager;

    public InstrumentationBuilder(string name, IServiceCollection services, IConfigurationSection configuration, RegistrationManager registrationManager)
    {
        this.name = name;
        this.services = services;
        this.configuration = configuration;
        this.registrationManager = registrationManager;
    }

    public void AddSetting<TSetting>()
        where TSetting : class
    {
        var section = configuration.GetSection(name);
        services.Configure<TSetting>(section);
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<TSetting>>().Value);
    }

    public void AddInstrumentation<TInstrumentation>()
        where TInstrumentation : class
    {
        services.AddSingleton<TInstrumentation>();
        registrationManager.Add(name, typeof(TInstrumentation));
    }
}
