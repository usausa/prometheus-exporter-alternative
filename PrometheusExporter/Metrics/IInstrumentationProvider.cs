namespace PrometheusExporter.Metrics;

internal interface IInstrumentationProvider
{
    public IReadOnlyList<Registration> Registrations { get; }

    void Setup();
}
