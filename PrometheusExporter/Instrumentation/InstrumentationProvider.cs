namespace PrometheusExporter.Instrumentation;

internal sealed class InstrumentationProvider : IInstrumentationProvider
{
    private readonly ILogger<InstrumentationProvider> log;

    private readonly IServiceProvider provider;

    private readonly IRegistrationManager registrationManager;

    public IEnumerable<Registration> Registrations => registrationManager.Registrations;

    public InstrumentationProvider(
        ILogger<InstrumentationProvider> log,
        IServiceProvider provider,
        IRegistrationManager registrationManager)
    {
        this.log = log;
        this.provider = provider;
        this.registrationManager = registrationManager;
    }

    public void Setup()
    {
        foreach (var registration in registrationManager.Registrations)
        {
            provider.GetRequiredService(registration.Type);

            log.InfoInstrumentationEnabled(registration.Name);
        }
    }
}
