namespace PrometheusExporter.Abstractions;

using System.Buffers;

public interface IMetric
{
    IGauge Create(params KeyValuePair<string, object?>[] tags);

    void Write(IBufferWriter<byte> writer, long timestamp);
}
