namespace PrometheusExporter.Metrics;

internal sealed class InstrumentationProvider : IInstrumentationProvider
{
    private readonly ILogger<InstrumentationProvider> log;

    private readonly IServiceProvider provider;

    public IReadOnlyList<Registration> Registrations { get; }

    public InstrumentationProvider(
        ILogger<InstrumentationProvider> log,
        IServiceProvider provider,
        List<Registration> registrations)
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

            log.InfoMetricsEnabled(registration.Name);
        }
    }
}
