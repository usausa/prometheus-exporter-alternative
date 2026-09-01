namespace PrometheusExporter.Abstractions;

public interface IInstrumentationRegistry
{
    void Register(string name, Action<IInstrumentationBuilder> configure);
}
