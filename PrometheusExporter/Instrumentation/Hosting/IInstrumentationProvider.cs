namespace PrometheusExporter.Instrumentation;

internal interface IInstrumentationProvider
{
    IEnumerable<Registration> Registrations { get; }

    void Setup();
}
