namespace PrometheusExporter.Abstractions;

using System.Buffers;

public interface IMetricManager
{
    IMetric CreateMetric(string name);

    void AddBeforeCollectCallback(Action callback);

    void AddBeforeCollectCallback(Func<CancellationToken, Task> callback);

    Task CollectAsync(IBufferWriter<byte> writer, CancellationToken cancel);
}
