namespace PrometheusExporter.Instrumentation.PerformanceCounter;

using System;
using System.Diagnostics;

using PrometheusExporter.Abstractions;

internal sealed class PerformanceCounterInstrumentation : IDisposable
{
    private readonly TimeSpan updateDuration;

    private readonly List<Counter> counters = [];

    private DateTime lastUpdate;

    public PerformanceCounterInstrumentation(IMetricManager manager, PerformanceCounterOptions options)
    {
        updateDuration = TimeSpan.FromMilliseconds(options.UpdateDuration);

        foreach (var entry in options.Counter)
        {
            var metric = manager.CreateMetric($"{options.Prefix}_{entry.Name}");

            foreach (var counter in CreateCounters(entry.Category, entry.Counter, entry.Instance))
            {
                counter.NextValue();

                var tags = new List<KeyValuePair<string, object?>>
                {
                    new("host", options.Host)
                };
                if (!String.IsNullOrEmpty(counter.InstanceName))
                {
                    tags.Add(new("name", counter.InstanceName));
                }

                var gauge = metric.CreateGauge(tags.ToArray());

                counters.Add(new Counter(gauge, counter));
            }
        }

        manager.AddBeforeCollectCallback(Update);
    }

    public void Dispose()
    {
        foreach (var counter in counters)
        {
            counter.Dispose();
        }
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

        foreach (var counter in counters)
        {
            counter.Update();
        }

        lastUpdate = now;
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private static IEnumerable<PerformanceCounter> CreateCounters(string category, string counter, string? instance = null)
    {
        if (!String.IsNullOrEmpty(instance))
        {
            yield return new PerformanceCounter(category, counter, instance);
        }
        else
        {
            var pcc = new PerformanceCounterCategory(category);
            if (pcc.CategoryType == PerformanceCounterCategoryType.SingleInstance)
            {
                yield return new PerformanceCounter(category, counter);
            }
            else
            {
                var names = pcc.GetInstanceNames();
                Array.Sort(names);
                foreach (var name in names)
                {
                    yield return new PerformanceCounter(category, counter, name);
                }
            }
        }
    }

    //--------------------------------------------------------------------------------
    // Counter
    //--------------------------------------------------------------------------------

    private sealed class Counter : IDisposable
    {
        private readonly IGauge gauge;

        private readonly PerformanceCounter counter;

        public Counter(IGauge gauge, PerformanceCounter counter)
        {
            this.gauge = gauge;
            this.counter = counter;
        }

        public void Dispose()
        {
            counter.Dispose();
        }

        public void Update()
        {
            gauge.Value = counter.NextValue();
        }
    }
}
