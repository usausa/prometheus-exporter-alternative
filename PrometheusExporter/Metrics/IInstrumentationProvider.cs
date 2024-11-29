namespace PrometheusExporter.Metrics;

internal interface IInstrumentationProvider
{
    public IEnumerable<Registration> Registrations { get; }

    void Setup();
}
