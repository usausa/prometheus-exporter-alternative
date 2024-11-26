namespace PrometheusExporter.Abstractions;

public interface IInstrumentationBuilder
{
    IInstrumentationBuilder AddInstrumentation<T>()
        where T : class;
}
