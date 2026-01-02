namespace PrometheusExporter.Instrumentation;

internal sealed class InstrumentationProvider : IInstrumentationProvider
{
    private readonly ILogger<InstrumentationProvider> log;

    private readonly IServiceProvider provider;

    public IEnumerable<Registration> Registrations { get; }

    public InstrumentationProvider(
        ILogger<InstrumentationProvider> log,
        IServiceProvider provider,
        IEnumerable<Registration> registrations)
    {
        this.log = log;
        this.provider = provider;
        Registrations = registrations;
    }

    public void Setup()
    {
        foreach (var registration in Registrations)
        {
            provider.GetRequiredService(registration.Type);

            log.InfoInstrumentationEnabled(registration.Name);
        }
    }
}
