namespace PrometheusExporter.Metrics;

using System.Buffers;

using PrometheusExporter.Abstractions;

internal sealed class MetricManager : IMetricManager, IDisposable
{
    private readonly List<IMetric> gauges = [];

    private readonly Dictionary<string, Metric> metricByName = [];

    private readonly SemaphoreSlim semaphore = new(1);

    private readonly List<Action> beforeCollectCallbacks = [];
    private readonly List<Func<CancellationToken, Task>> beforeCollectAsyncCallbacks = [];

    public void Dispose()
    {
        semaphore.Dispose();
    }

    public IMetric CreateGauge(string name, string? sort = null) => CreateMetric("gauge", name, sort);

    public IMetric CreateCounter(string name, string? sort = null) => CreateMetric("counter", name, sort);

    private Metric CreateMetric(string type, string name, string? sort)
    {
        semaphore.Wait();
        try
        {
            if (metricByName.TryGetValue(name, out var metric))
            {
                if (metric.Type != type)
                {
                    throw new InvalidOperationException($"Metric is already registered with a different type. name=[{name}], registered=[{metric.Type}], requested=[{type}]");
                }

                return metric;
            }

            metric = new Metric(type, name, sort);
            metricByName.Add(name, metric);
            gauges.Add(metric);
            return metric;
        }
        finally
        {
            semaphore.Release();
        }
    }

    public void AddBeforeCollectCallback(Action callback)
    {
        beforeCollectCallbacks.Add(callback);
    }

    public void AddBeforeCollectCallback(Func<CancellationToken, Task> callback)
    {
        beforeCollectAsyncCallbacks.Add(callback);
    }

    public async Task CollectAsync(IBufferWriter<byte> writer, long timestamp, CancellationToken cancel)
    {
        await semaphore.WaitAsync(cancel).ConfigureAwait(false);
        try
        {
            foreach (var callback in beforeCollectCallbacks)
            {
                callback();
            }

            await Task.WhenAll(beforeCollectAsyncCallbacks.Select(callback => callback(cancel))).ConfigureAwait(false);

            foreach (var gauge in gauges)
            {
                gauge.Write(writer, timestamp);
            }

            Helper.WriteEof(writer);
        }
        finally
        {
            semaphore.Release();
        }
    }
}
