namespace PrometheusExporter.Abstractions;

public interface IGauge
{
    double Value { get; set; }

    void Remove();
}
