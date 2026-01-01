namespace PrometheusExporter.Instrumentation;

using PrometheusExporter.Abstractions;

internal sealed class InstrumentationBuilder : IInstrumentationBuilder
{
    private readonly IServiceCollection services;

    private readonly List<Registration> registrations;

    public InstrumentationBuilder(IServiceCollection services, List<Registration> registrations)
    {
        this.services = services;
        this.registrations = registrations;
    }

    public IInstrumentationBuilder AddInstrumentation<T>(string name)
        where T : class
    {
        services.AddSingleton<T>();
        registrations.Add(new Registration(name, typeof(T)));
        return this;
    }

    public IInstrumentationBuilder AddInstrumentation<T>(string name, Func<IServiceProvider, T> factory)
        where T : class
    {
        services.AddSingleton(factory);
        registrations.Add(new Registration(name, typeof(T)));
        return this;
    }
}
