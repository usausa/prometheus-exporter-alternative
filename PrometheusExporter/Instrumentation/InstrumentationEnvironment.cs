namespace PrometheusExporter.Instrumentation;

using PrometheusExporter.Abstractions;

internal sealed class InstrumentationEnvironment : IInstrumentationEnvironment
{
    public string Host { get; }

    public InstrumentationEnvironment(string host)
    {
        Host = host;
    }
}
