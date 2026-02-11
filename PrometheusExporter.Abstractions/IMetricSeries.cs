namespace PrometheusExporter.Abstractions;

public interface IMetricSeries
{
    double Value { get; set; }

    void Remove();
}
