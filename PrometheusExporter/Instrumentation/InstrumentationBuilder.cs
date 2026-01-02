namespace PrometheusExporter.Instrumentation;

using PrometheusExporter.Abstractions;

internal sealed class InstrumentationBuilder : IInstrumentationBuilder
{
    public void AddSetting<TSetting>()
        where TSetting : class
    {
        throw new NotImplementedException();
    }

    public void AddInstrumentation<TInstrumentation>()
        where TInstrumentation : class
    {
        throw new NotImplementedException();
    }
}
