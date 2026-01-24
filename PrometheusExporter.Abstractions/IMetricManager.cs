namespace PrometheusExporter.Abstractions;

using System.Buffers;

public interface IMetricManager
{
    IMetric CreateGauge(string name, string? sort = null);

    IMetric CreateCounter(string name, string? sort = null);

    void AddBeforeCollectCallback(Action callback);

    void AddBeforeCollectCallback(Func<CancellationToken, Task> callback);

    Task CollectAsync(IBufferWriter<byte> writer, long timestamp, CancellationToken cancel);
}
