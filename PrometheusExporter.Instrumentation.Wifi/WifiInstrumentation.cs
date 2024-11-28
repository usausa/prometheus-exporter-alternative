namespace PrometheusExporter.Instrumentation.Wifi;

using System;

using ManagedNativeWifi;

using PrometheusExporter.Abstractions;

internal sealed class WifiInstrumentation
{
    private readonly TimeSpan updateDuration;

    private readonly string host;

    private readonly int signalThreshold;

    private readonly bool knownOnly;

    private readonly HashSet<string> knownAccessPoints;

    private readonly IMetric metric;

    private readonly List<AccessPoint> accessPoints = [];

    private DateTime lastUpdate;

    public WifiInstrumentation(IMetricManager manager, WifiOptions options)
    {
        updateDuration = TimeSpan.FromMilliseconds(options.UpdateDuration);
        host = options.Host;
        signalThreshold = options.SignalThreshold;
        knownOnly = options.KnownOnly;
        knownAccessPoints = options.KnownAccessPoint.Select(NormalizeAddress).ToHashSet();

        metric = manager.CreateMetric("wifi_rssi");

        manager.AddBeforeCollectCallback(Update);
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

        // Prepare update
        foreach (var ap in accessPoints)
        {
            ap.Detected = false;
        }

        var added = false;
        // ReSharper disable once LoopCanBeConvertedToQuery
        foreach (var network in NativeWifi.EnumerateBssNetworks())
        {
            if (network.SignalStrength <= signalThreshold)
            {
                continue;
            }

            var bssid = network.Bssid.ToString();
            if (knownOnly && !knownAccessPoints.Contains(bssid))
            {
                continue;
            }

            var ap = default(AccessPoint);
            foreach (var accessPoint in accessPoints)
            {
                if (accessPoint.Bssid == bssid)
                {
                    ap = accessPoint;
                    break;
                }
            }

            if (ap is null)
            {
                ap = new AccessPoint(bssid, network.Ssid.ToString(), metric.CreateGauge(MakeTags(network)));
                accessPoints.Add(ap);
                added = true;
            }

            ap.Gauge.Value = network.SignalStrength;
        }

        // Post update
        for (var i = accessPoints.Count - 1; i >= 0; i--)
        {
            var ap = accessPoints[i];
            if (!ap.Detected)
            {
                accessPoints.RemoveAt(i);
                ap.Gauge.Remove();
            }
        }

        if (added)
        {
            accessPoints.Sort(static (x, y) => String.Compare(x.Ssid, y.Ssid, StringComparison.Ordinal));
        }

        lastUpdate = now;
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private static string NormalizeAddress(string address)
    {
        var value = Convert.ToUInt64(address.Replace(":", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal), 16);
        return $"{(value >> 48) & 0xFF:X2}:{(value >> 32) & 0xFF:X2}:{(value >> 24) & 0xFF:X2}:{(value >> 16) & 0xFF:X2}:{(value >> 8) & 0xFF:X2}:{value & 0xFF:X2}";
    }

    private KeyValuePair<string, object?>[] MakeTags(BssNetworkPack network) =>
    [
        new("host", host),
        new("ssid", network.Ssid.ToString()),
        new("bssid", network.Bssid.ToString()),
        new("protocol", network.PhyType.ToProtocolName()),
        new("band", network.Band),
        new("channel", network.Channel)
    ];

    //--------------------------------------------------------------------------------
    // AccessPoint
    //--------------------------------------------------------------------------------

    private sealed class AccessPoint
    {
        public bool Detected { get; set; }

        public string Bssid { get; }

        public string Ssid { get; }

        public IGauge Gauge { get; }

        public AccessPoint(string bssid, string ssid, IGauge gauge)
        {
            Bssid = bssid;
            Ssid = ssid;
            Gauge = gauge;
        }
    }
}
