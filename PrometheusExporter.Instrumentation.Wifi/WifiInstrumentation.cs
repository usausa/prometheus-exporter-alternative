namespace PrometheusExporter.Instrumentation.Wifi;

using System;

using ManagedNativeWifi;

using PrometheusExporter.Abstractions;

internal sealed class WifiInstrumentation
{
    private readonly string host;

    private readonly TimeSpan updateDuration;

    private readonly int signalThreshold;

    private readonly bool knownOnly;

    private readonly HashSet<string> knownAccessPoints;

    private readonly IMetric metric;

    private readonly List<AccessPoint> accessPoints = [];

    private DateTime lastUpdate;

    public WifiInstrumentation(
        IInstrumentationEnvironment environment,
        WifiOptions options,
        IMetricManager manager)
    {
        host = environment.Host;
        updateDuration = TimeSpan.FromMilliseconds(options.UpdateDuration);
        signalThreshold = options.SignalThreshold;
        knownOnly = options.KnownOnly;
        knownAccessPoints = options.KnownAccessPoint.Select(NormalizeAddress).ToHashSet();

        metric = manager.CreateGauge("wifi_rssi", "ssid");

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

        // Update
#pragma warning disable CA1031
        try
        {
            foreach (var network in NativeWifi.EnumerateBssNetworks())
            {
                if (network.Rssi <= signalThreshold)
                {
                    continue;
                }

                var bssid = network.Bssid.ToString();
                var ssid = network.Ssid.ToString();

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

                if ((ap is not null) && (ap.Ssid != ssid))
                {
                    ap = null;
                }

                if (ap is null)
                {
                    ap = new AccessPoint(bssid, ssid, metric.Create(MakeTags(network)));
                    accessPoints.Add(ap);
                }

                ap.Detected = true;
                ap.Rssi.Value = network.Rssi;
            }
        }
        catch
        {
            // Ignore errors
        }
#pragma warning restore CA1031

        // Post update
        for (var i = accessPoints.Count - 1; i >= 0; i--)
        {
            var ap = accessPoints[i];
            if (!ap.Detected)
            {
                accessPoints.RemoveAt(i);
                ap.Unregister();
            }
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

        public IGauge Rssi { get; }

        public AccessPoint(string bssid, string ssid, IGauge rssi)
        {
            Bssid = bssid;
            Ssid = ssid;
            Rssi = rssi;
        }

        public void Unregister()
        {
            Rssi.Remove();
        }
    }
}
