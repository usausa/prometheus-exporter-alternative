namespace PrometheusExporter.Abstractions;

#pragma warning disable CA1711
public interface IInstrumentationRegistry
{
    void Register(string name, Action<IInstrumentationBuilder> configure);
}
#pragma warning restore CA1711
