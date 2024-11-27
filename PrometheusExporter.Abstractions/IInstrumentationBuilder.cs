namespace PrometheusExporter.Abstractions;

public interface IInstrumentationBuilder
{
    IInstrumentationBuilder AddInstrumentation<T>(string name)
        where T : class;

    IInstrumentationBuilder AddInstrumentation<T>(string name, Func<IServiceProvider, T> factory)
        where T : class;
}
