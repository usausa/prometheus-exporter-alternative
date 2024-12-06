namespace PrometheusExporter.Metrics;

using PrometheusExporter.Abstractions;

internal sealed class Gauge : IGauge
{
    private readonly Metric parent;

    public double Value
    {
        get => Interlocked.Exchange(ref field, field);
        set => Interlocked.Exchange(ref field, value);
    }

    public string? SortKey { get; }

    public Tag[] Tags { get; }

    public Gauge(Metric parent, string? sortKey, Tag[] tags)
    {
        this.parent = parent;
        SortKey = sortKey;
        Tags = tags;
    }

    public void Remove()
    {
        parent.Unregister(this);
    }
}
