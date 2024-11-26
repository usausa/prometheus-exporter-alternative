namespace PrometheusExporter.Metrics;

using System.Buffers;

using PrometheusExporter.Abstractions;

internal sealed class Gauge : IGauge
{
    private readonly Metric parent;

    private readonly Tag[] tags;

    public double Value
    {
        get => Interlocked.Exchange(ref field, field);
        set => Interlocked.Exchange(ref field, value);
    }

    public Gauge(Metric parent, KeyValuePair<string, object?>[] tags)
    {
        this.parent = parent;
        this.tags = Helper.PrepareTags(tags);
    }

    public void Remove()
    {
        parent.Unregister(this);
    }

    internal void Write(IBufferWriter<byte> writer, string name)
    {
        Helper.WriteValue(writer, name, Value, tags);
    }
}
