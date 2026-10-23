namespace PrometheusExporter.Instrumentation.PerformanceCounter;

using System;
using System.Diagnostics;
using System.Globalization;

using PrometheusExporter.Abstractions;
using PrometheusExporter.Infrastructure.Windows.Pdh;

internal sealed class PerformanceCounterInstrumentation : IDisposable
{
    private readonly string host;

    private readonly TimeSpan updateDuration;

    private readonly PdhQuery query;

    private readonly List<CounterSet> counterSets = [];

    private DateTime lastUpdate;

    public PerformanceCounterInstrumentation(
        PerformanceCounterOptions options,
        IInstrumentationEnvironment environment,
        IMetricManager manager)
    {
        host = environment.Host;
        updateDuration = TimeSpan.FromMilliseconds(options.UpdateDuration);

        query = PdhQuery.Create() ?? throw new InvalidOperationException("Counter query could not be opened.");

        foreach (var entry in options.Counter)
        {
            var metric = entry.Type == CounterType.Counter
                ? manager.CreateCounter(entry.Name)
                : manager.CreateGauge(entry.Name);

            foreach (var path in ResolvePaths(entry))
            {
                var counter = query.AddCounterArray(path);
                if (counter is null)
                {
                    throw new InvalidOperationException($"Counter is not available. path=[{path}]");
                }

                counterSets.Add(new CounterSet(host, metric, counter));
            }
        }

        // Takes the baseline that a rate counter needs
        _ = query.Collect();

        manager.AddBeforeCollectCallback(Update);
    }

    public void Dispose()
    {
        query.Dispose();
    }

    //--------------------------------------------------------------------------------
    // Event
    //--------------------------------------------------------------------------------

    private void Update()
    {
        var now = DateTime.Now;
        if ((now - lastUpdate) < updateDuration)
        {
            return;
        }

        if (query.Collect())
        {
            foreach (var counterSet in counterSets)
            {
                counterSet.Update();
            }
        }

        lastUpdate = now;
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    // A wildcard is resolved on every collect, so instances are followed as they come and go. It cannot be
    // used for a category whose instance names repeat: the performance library tells them apart with a "#1"
    // suffix and PDH does not, so every duplicate would land on the same tag value.
    private static IEnumerable<string> ResolvePaths(CounterEntry entry)
    {
        if (!String.IsNullOrEmpty(entry.Instance))
        {
            yield return MakePath(entry.Category, entry.Counter, entry.Instance);
            yield break;
        }

        var category = new PerformanceCounterCategory(entry.Category);
        if (category.CategoryType == PerformanceCounterCategoryType.SingleInstance)
        {
            yield return MakePath(entry.Category, entry.Counter, null);
            yield break;
        }

        var names = category.GetInstanceNames();
        if (!Array.Exists(names, static x => x.Contains('#', StringComparison.Ordinal)))
        {
            yield return MakePath(entry.Category, entry.Counter, "*");
            yield break;
        }

        Array.Sort(names);
        foreach (var name in names)
        {
            yield return MakePath(entry.Category, entry.Counter, name);
        }
    }

    private static string MakePath(string category, string counter, string? instance) =>
        instance is null
            ? String.Create(CultureInfo.InvariantCulture, $@"\{category}\{counter}")
            : String.Create(CultureInfo.InvariantCulture, $@"\{category}({instance})\{counter}");

    //--------------------------------------------------------------------------------
    // CounterSet
    //--------------------------------------------------------------------------------

    private sealed class CounterSet
    {
        private readonly string host;

        private readonly IMetric metric;

        private readonly PdhCounterArray counter;

        private readonly List<InstanceEntry> entries = [];

        public CounterSet(string host, IMetric metric, PdhCounterArray counter)
        {
            this.host = host;
            this.metric = metric;
            this.counter = counter;
        }

        public void Update()
        {
            // Prepare update
            foreach (var entry in entries)
            {
                entry.Detected = false;
            }

            // Update
            for (var i = 0; i < counter.Count; i++)
            {
                var name = counter.GetName(i);
                var entry = Find(name) ?? Add(name);

                entry.Detected = true;
                entry.Series.Value = counter.HasValue(i) ? counter.GetValue(i) : Double.NaN;
            }

            // Post update
            for (var i = entries.Count - 1; i >= 0; i--)
            {
                var entry = entries[i];
                if (!entry.Detected)
                {
                    entries.RemoveAt(i);
                    entry.Series.Remove();
                }
            }
        }

        // Compared against the span over the read buffer, so a known instance creates no string
        private InstanceEntry? Find(ReadOnlySpan<char> name)
        {
            foreach (var entry in entries)
            {
                if (name.SequenceEqual(entry.Name))
                {
                    return entry;
                }
            }

            return null;
        }

        private InstanceEntry Add(ReadOnlySpan<char> name)
        {
            var instanceName = new string(name);

            var tags = instanceName.Length > 0
                ? [new KeyValuePair<string, object?>("host", host), new KeyValuePair<string, object?>("name", instanceName)]
                : new KeyValuePair<string, object?>[] { new("host", host) };

            var entry = new InstanceEntry(instanceName, metric.Create(tags));
            entries.Add(entry);
            return entry;
        }
    }

    //--------------------------------------------------------------------------------
    // InstanceEntry
    //--------------------------------------------------------------------------------

    private sealed class InstanceEntry
    {
        public string Name { get; }

        public IMetricSeries Series { get; }

        public bool Detected { get; set; }

        public InstanceEntry(string name, IMetricSeries series)
        {
            Name = name;
            Series = series;
        }
    }
}
