namespace PrometheusExporter.Abstractions;

public interface IInstrumentationBuilder
{
    void AddSetting<TSetting>()
        where TSetting : class;

    void AddInstrumentation<TInstrumentation>()
        where TInstrumentation : class;
}
