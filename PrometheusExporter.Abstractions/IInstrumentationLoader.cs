namespace PrometheusExporter.Abstractions;

public interface IInstrumentationLoader
{
    void Load(IInstrumentationRegistry registry);
}
