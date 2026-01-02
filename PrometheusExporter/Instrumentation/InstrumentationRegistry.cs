namespace PrometheusExporter.Instrumentation;

using PrometheusExporter.Abstractions;

internal sealed class InstrumentationRegistry : IInstrumentationRegistry
{
    public void Register(string name, Action<IInstrumentationBuilder> configure)
    {
        throw new NotImplementedException();
    }
}
