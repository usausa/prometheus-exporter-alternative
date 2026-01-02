namespace PrometheusExporter.Metrics;

using System.Buffers;

using PrometheusExporter.Abstractions;

internal sealed class Metric : IMetric
{
    private readonly string name;

    private readonly string? sort;

    private readonly Lock sync = new();

    private readonly List<Gauge> entries = [];

    public Metric(string name, string? sort)
    {
        this.name = name;
        this.sort = sort;
    }

    void IMetric.Write(IBufferWriter<byte> writer, long timestamp)
    {
        lock (sync)
        {
            var pooledBuffer = default(double[]?);
            var values = entries.Count <= 64 ? stackalloc double[entries.Count] : (pooledBuffer = ArrayPool<double>.Shared.Rent(entries.Count)).AsSpan();

            for (var i = 0; i < entries.Count; i++)
            {
                values[i] = entries[i].Value;
            }

            var hasValue = false;
            for (var i = 0; i < entries.Count; i++)
            {
                if (Double.IsFinite(values[i]))
                {
                    hasValue = true;
                    break;
                }
            }

            if (hasValue)
            {
                Helper.WriteType(writer, name);

                for (var i = 0; i < entries.Count; i++)
                {
                    var value = values[i];
                    if (Double.IsFinite(value))
                    {
                        Helper.WriteValue(writer, timestamp, name, value, entries[i].Tags);
                    }
                }
            }

            if (pooledBuffer is not null)
            {
                ArrayPool<double>.Shared.Return(pooledBuffer);
            }
        }
    }

    public IGauge CreateGauge(params KeyValuePair<string, object?>[] tags)
    {
        lock (sync)
        {
            var stringTags = Helper.ConvertTags(tags);
            var sortKey = sort is not null ? stringTags.FirstOrDefault(x => x.Key == sort)?.Value : null;
            var gauge = new Gauge(this, sortKey, stringTags);
            entries.Add(gauge);

            if (sort is not null)
            {
                entries.Sort(TagComparer.Instance);
            }

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

    private sealed class TagComparer : IComparer<Gauge>
    {
        public static TagComparer Instance { get; } = new();

        public int Compare(Gauge? x, Gauge? y)
        {
            var key1 = x!.SortKey;
            var key2 = y!.SortKey;

            if ((key1 is null) && (key2 is null))
            {
                return 0;
            }
            if (key1 is null)
            {
                return -1;
            }
            if (key2 is null)
            {
                return 1;
            }
            return String.CompareOrdinal(key1, key2);
        }
    }
}
