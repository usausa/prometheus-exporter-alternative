namespace PrometheusExporter.Infrastructure.Linux.BlueZ;

using System.Collections.Concurrent;

using Tmds.DBus;

public enum BleScanEventType
{
    Discover,
    Update,
    Lost
}

public sealed class BleScanEvent
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;

    public BleScanEventType Type { get; init; }

    public string DevicePath { get; init; } = string.Empty;

    public IReadOnlyCollection<string> Keys { get; init; } = [];

    public string? Address { get; init; }

    public string? Name { get; init; }

    public string? Alias { get; init; }

    public short? Rssi { get; init; }

    public IReadOnlyDictionary<ushort, byte[]>? ManufacturerData { get; init; }
}

public sealed class BleScanSession : IAsyncDisposable
{
#pragma warning disable CA1003
    public event Action<BleScanEvent>? DeviceEvent;
#pragma warning restore CA1003

    private readonly Connection connection;
    private readonly IObjectManager objectManager;
    private readonly IAdapter1 adapter;

    private IDisposable? addedSubscription;
    private IDisposable? removedSubscription;

    private readonly ConcurrentDictionary<ObjectPath, IDisposable> devicePropertySubscriptions = new();

    private volatile bool discovering;

    private BleScanSession(Connection connection, IObjectManager objectManager, IAdapter1 adapter)
    {
        this.connection = connection;
        this.objectManager = objectManager;
        this.adapter = adapter;
    }

    public static async ValueTask<BleScanSession> CreateAsync()
    {
        var con = new Connection(Address.System);
        await con.ConnectAsync().ConfigureAwait(false);

        var manager = con.CreateProxy<IObjectManager>("org.bluez", new ObjectPath("/"));
        var objects = await manager.GetManagedObjectsAsync().ConfigureAwait(false);
        var adapterPath = objects.Keys.FirstOrDefault(p => objects[p].ContainsKey("org.bluez.Adapter1"));
        if (adapterPath == default)
        {
            throw new InvalidOperationException("Bluetooth adapter (org.bluez.Adapter1) not found.");
        }

        var adapter = con.CreateProxy<IAdapter1>("org.bluez", adapterPath);

        return new BleScanSession(con, manager, adapter);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        connection.Dispose();
    }

    private void RaiseEvent(BleScanEvent e) => DeviceEvent?.Invoke(e);

    public async Task StartAsync()
    {
        if (discovering)
        {
            return;
        }

        // Device added subscription
        addedSubscription = await objectManager.WatchInterfacesAddedAsync(ev =>
        {
            if (!ev.Interfaces.TryGetValue("org.bluez.Device1", out var props))
            {
                return;
            }

            RaiseEvent(new BleScanEvent
            {
                Timestamp = DateTimeOffset.Now,
                Type = BleScanEventType.Discover,
                DevicePath = ev.ObjectPath.ToString(),
                Keys = props.Keys.ToArray(),
                Address = TryGetString(props, "Address"),
                Name = TryGetString(props, "Name"),
                Alias = TryGetString(props, "Alias"),
                Rssi = TryGetInt16(props, "RSSI")
            });

#pragma warning disable CA2012
            _ = SubscribeDevicePropertyAsync(ev.ObjectPath);
#pragma warning restore CA2012
        }).ConfigureAwait(false);
        // Device removed subscription
        removedSubscription = await objectManager.WatchInterfacesRemovedAsync(ev =>
        {
            if (devicePropertySubscriptions.TryRemove(ev.ObjectPath, out var subscription))
            {
                subscription.Dispose();
            }

            RaiseEvent(new BleScanEvent
            {
                Timestamp = DateTimeOffset.Now,
                Type = BleScanEventType.Lost,
                DevicePath = ev.ObjectPath.ToString(),
                Keys = ev.Interfaces.ToArray()
            });
        }).ConfigureAwait(false);

        var objects = await objectManager.GetManagedObjectsAsync().ConfigureAwait(false);
        foreach (var (key, value) in objects)
        {
            if (!value.TryGetValue("org.bluez.Device1", out var props))
            {
                continue;
            }

            RaiseEvent(new BleScanEvent
            {
                Timestamp = DateTimeOffset.Now,
                Type = BleScanEventType.Discover,
                DevicePath = key.ToString(),
                Keys = props.Keys.ToArray(),
                Address = TryGetString(props, "Address"),
                Name = TryGetString(props, "Name"),
                Alias = TryGetString(props, "Alias"),
                Rssi = TryGetInt16(props, "RSSI")
            });

#pragma warning disable CA2012
            _ = SubscribeDevicePropertyAsync(key);
#pragma warning restore CA2012
        }

        await adapter.StartDiscoveryAsync().ConfigureAwait(false);

        discovering = true;
    }

    private async ValueTask SubscribeDevicePropertyAsync(ObjectPath devicePath)
    {
        if (devicePropertySubscriptions.ContainsKey(devicePath))
        {
            return;
        }

        var properties = connection.CreateProxy<IProperties>("org.bluez", devicePath);
        var subscription = await properties.WatchPropertiesChangedAsync(ev =>
        {
            if (!String.Equals(ev.Interface, "org.bluez.Device1", StringComparison.Ordinal))
            {
                return;
            }

            if (ev.Changed.Count == 0)
            {
                return;
            }

            var props = ev.Changed;
            var md = TryGetManufacturerData(props);
            RaiseEvent(new BleScanEvent
            {
                Timestamp = DateTimeOffset.Now,
                Type = BleScanEventType.Update,
                DevicePath = devicePath.ToString(),
                Keys = props.Keys.ToArray(),
                Address = TryGetString(props, "Address"),
                Name = TryGetString(props, "Name"),
                Alias = TryGetString(props, "Alias"),
                Rssi = TryGetInt16(props, "RSSI"),
                ManufacturerData = md
            });
        }).ConfigureAwait(false);

        if (!devicePropertySubscriptions.TryAdd(devicePath, subscription))
        {
            subscription.Dispose();
        }
    }

    public async ValueTask StopAsync()
    {
        if (!discovering)
        {
            return;
        }

#pragma warning disable CA1031
        try
        {
            await adapter.StopDiscoveryAsync().ConfigureAwait(false);
        }
        catch
        {
            // Ignore
        }
#pragma warning restore CA1031

        addedSubscription?.Dispose();
        addedSubscription = null;

        removedSubscription?.Dispose();
        removedSubscription = null;

        foreach (var (_, value) in devicePropertySubscriptions)
        {
            value.Dispose();
        }
        devicePropertySubscriptions.Clear();

        discovering = false;
    }

    private static string? TryGetString(IDictionary<string, object>? props, string key)
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (props == null || !props.TryGetValue(key, out var value) || (value is null))
        {
            return null;
        }

        return value as string;
    }

    private static short? TryGetInt16(IDictionary<string, object>? props, string key)
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (props == null || !props.TryGetValue(key, out var value) || (value is null))
        {
            return null;
        }

        return value switch
        {
            short s => s,
            int i => (short)i,
            long l => (short)l,
            _ => null
        };
    }

    private static Dictionary<ushort, byte[]>? TryGetManufacturerData(IDictionary<string, object> props)
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (!props.TryGetValue("ManufacturerData", out var value) || (value is null))
        {
            return null;
        }

        if (value is IDictionary<ushort, byte[]> direct)
        {
            return new Dictionary<ushort, byte[]>(direct);
        }

        if (value is IDictionary<ushort, object> objectDictionary)
        {
            var res = new Dictionary<ushort, byte[]>();
            foreach (var (key, obj) in objectDictionary)
            {
                if (obj is byte[] bytes)
                {
                    res[key] = bytes;
                }
                else if (obj is IEnumerable<byte> eb)
                {
                    res[key] = eb.ToArray();
                }
            }
            return res;
        }

        return null;
    }
}
