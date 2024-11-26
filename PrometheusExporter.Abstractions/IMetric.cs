namespace PrometheusExporter.Abstractions;

using System.Buffers;

public interface IMetric
{
    IGauge CreateGauge(params KeyValuePair<string, object?>[] tags);

    void Write(IBufferWriter<byte> writer);
}
