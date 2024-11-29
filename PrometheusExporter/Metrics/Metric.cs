namespace PrometheusExporter.Metrics;

using System.Buffers;

using PrometheusExporter.Abstractions;

internal sealed class Metric : IMetric
{
    private readonly string name;

    private readonly object sync = new();

    private readonly List<Gauge> entries = [];

    public Metric(string name)
    {
        this.name = name;
    }

    void IMetric.Write(IBufferWriter<byte> writer, long timestamp)
    {
        lock (sync)
        {
            if (entries.Count > 0)
            {
                Helper.WriteType(writer, name);

                foreach (var entry in entries)
                {
                    entry.Write(writer, timestamp, name);
                }
            }
        }
    }

    public IGauge CreateGauge(params KeyValuePair<string, object?>[] tags)
    {
        lock (sync)
        {
            var gauge = new Gauge(this, tags);
            entries.Add(gauge);
            return gauge;
        }
    }

    internal void Unregister(Gauge entry)
    {
        lock (sync)
        {
            entries.Remove(entry);
        }
    }
}
