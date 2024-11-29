namespace PrometheusExporter.Metrics;

using System.Buffers;

using PrometheusExporter.Abstractions;

internal sealed class MetricManager : IMetricManager, IDisposable
{
    private readonly List<IMetric> gauges = [];

    private readonly SemaphoreSlim semaphore = new(1);

    private readonly List<Action> beforeCollectCallbacks = [];
    private readonly List<Func<CancellationToken, Task>> beforeCollectAsyncCallbacks = [];

    public void Dispose()
    {
        semaphore.Dispose();
    }

    public IMetric CreateMetric(string name)
    {
        var gauge = new Metric(name);

        semaphore.Wait(0);
        try
        {
            gauges.Add(gauge);
        }
        finally
        {
            semaphore.Release();
        }

        return gauge;
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
        await semaphore.WaitAsync(0, cancel).ConfigureAwait(false);
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
