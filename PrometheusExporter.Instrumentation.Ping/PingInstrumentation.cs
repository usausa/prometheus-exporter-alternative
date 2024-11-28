namespace PrometheusExporter.Instrumentation.Ping;

using System.Net;
using System.Net.NetworkInformation;

using PrometheusExporter.Abstractions;

internal sealed class PingInstrumentation : IDisposable
{
    private readonly Target[] targets;

    private readonly Timer timer;

    public PingInstrumentation(IMetricManager manager, PingOptions options)
    {
        var timeMetric = manager.CreateMetric("ping_result_time");

        targets = options.Target
            .Select(x => new Target(
                timeMetric.CreateGauge(MakeTags(options.Host, x)),
                options.Timeout,
                Dns.GetHostAddresses(x.Address)[0]))
            .ToArray();

        timer = new Timer(Update, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(options.Interval));
    }

    public void Dispose()
    {
        timer.Dispose();
        foreach (var target in targets)
        {
            target.Dispose();
        }
    }

    //--------------------------------------------------------------------------------
    // Event
    //--------------------------------------------------------------------------------

    // ReSharper disable once AsyncVoidMethod
    private async void Update(object? state)
    {
        await Task.WhenAll(targets.Select(async static x => await x.UpdateAsync()));
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private static KeyValuePair<string, object?>[] MakeTags(string host, TargetEntry entry) =>
        [new("host", host), new("address", entry.Address), new("name", entry.Name ?? entry.Address)];

    //--------------------------------------------------------------------------------
    // Target
    //--------------------------------------------------------------------------------

    private sealed class Target : IDisposable
    {
        private readonly IGauge time;

        private readonly Ping ping = new();

        private readonly int timeout;

        private readonly IPAddress address;

        public Target(IGauge time, int timeout, IPAddress address)
        {
            this.time = time;
            this.timeout = timeout;
            this.address = address;
        }

        public void Dispose()
        {
            ping.Dispose();
        }

#pragma warning disable CA1031
        public async ValueTask UpdateAsync()
        {
            try
            {
                var result = await ping.SendPingAsync(address, timeout);
                time.Value = result.Status == IPStatus.Success ? result.RoundtripTime : double.NaN;
            }
            catch
            {
                time.Value = double.NaN;
            }
        }
#pragma warning restore CA1031
    }
}
