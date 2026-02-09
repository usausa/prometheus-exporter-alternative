namespace PrometheusExporter.Infrastructure.Linux.BlueZ;

using Tmds.DBus;

[DBusInterface("org.freedesktop.DBus.ObjectManager")]
public interface IObjectManager : IDBusObject
{
    Task<IDictionary<ObjectPath, IDictionary<string, IDictionary<string, object>>>> GetManagedObjectsAsync();

    Task<IDisposable> WatchInterfacesAddedAsync(Action<(ObjectPath ObjectPath, IDictionary<string, IDictionary<string, object>> Interfaces)> handler);

    Task<IDisposable> WatchInterfacesRemovedAsync(Action<(ObjectPath ObjectPath, string[] Interfaces)> handler);
}

[DBusInterface("org.bluez.Adapter1")]
public interface IAdapter1 : IDBusObject
{
    Task StartDiscoveryAsync();

    Task StopDiscoveryAsync();
}

[DBusInterface("org.freedesktop.DBus.Properties")]
public interface IProperties : IDBusObject
{
    Task<IDisposable> WatchPropertiesChangedAsync(Action<(string Interface, IDictionary<string, object> Changed, string[] Invalidated)> handler);
}
