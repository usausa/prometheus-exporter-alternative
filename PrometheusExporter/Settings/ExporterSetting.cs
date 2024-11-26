namespace PrometheusExporter.Settings;

#if WINDOWS_TELEMETRY
using PrometheusExporter.Instrumentation.Ble;
#endif
#if WINDOWS_TELEMETRY
using PrometheusExporter.Instrumentation.DiskInfo;
#endif
#if WINDOWS_TELEMETRY
using PrometheusExporter.Instrumentation.HardwareMonitor;
#endif
#if WINDOWS_TELEMETRY
using PrometheusExporter.Instrumentation.HyperV;
#endif
#if WINDOWS_TELEMETRY
using PrometheusExporter.Instrumentation.PerformanceCounter;
#endif
using PrometheusExporter.Instrumentation.SensorOmron;
#if WINDOWS_TELEMETRY
using PrometheusExporter.Instrumentation.SwitchBot;
#endif
using PrometheusExporter.Instrumentation.Ping;
using PrometheusExporter.Instrumentation.WFWattch2;
#if WINDOWS_TELEMETRY
using PrometheusExporter.Instrumentation.Wifi;
#endif

#pragma warning disable CA1819
public sealed class ExporterSetting
{
    // Application

    public string EndPoint { get; set; } = default!;

    public string? Host { get; set; }

    public bool EnableApplication { get; set; }

    // Ble

#if WINDOWS_TELEMETRY
    public bool EnableBle { get; set; }

    public BleOptions Ble { get; set; } = new();
#endif

    // DiskInfo

#if WINDOWS_TELEMETRY
    public bool EnableDiskInfo { get; set; }

    public DiskInfoOptions DiskInfo { get; set; } = new();
#endif

    // HardwareMonitor

#if WINDOWS_TELEMETRY
    public bool EnableHardwareMonitor { get; set; }

    public HardwareMonitorOptions HardwareMonitor { get; set; } = new();
#endif

    // HardwareMonitor

#if WINDOWS_TELEMETRY
    public bool EnableHyperV { get; set; }

    public HyperVOptions HyperV { get; set; } = new();
#endif

    // PerformanceCounter

#if WINDOWS_TELEMETRY
    public bool EnablePerformanceCounter { get; set; }

    public PerformanceCounterOptions PerformanceCounter { get; set; } = new();
#endif

    // Ping

    public bool EnablePing { get; set; }

    public PingOptions Ping { get; set; } = new();

    // SensorOmron

    public bool EnableSensorOmron { get; set; }

    public SensorOmronOptions SensorOmron { get; set; } = new();

    // SwitchBot

#if WINDOWS_TELEMETRY
    public bool EnableSwitchBot { get; set; }

    public SwitchBotOptions SwitchBot { get; set; } = new();
#endif

    // WFWattch2

    public bool EnableWFWattch2 { get; set; }

    public WFWattch2Options WFWattch2 { get; set; } = new();

    // Wifi

#if WINDOWS_TELEMETRY
    public bool EnableWifi { get; set; }

    public WifiOptions Wifi { get; set; } = new();
#endif
}
#pragma warning restore CA1819
