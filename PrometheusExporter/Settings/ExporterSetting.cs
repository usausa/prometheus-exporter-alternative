namespace PrometheusExporter.Settings;

#if WINDOWS_EXPORTER
using PrometheusExporter.Instrumentation.Ble;
#endif
#if WINDOWS_EXPORTER
using PrometheusExporter.Instrumentation.BTWattch2;
#endif
#if WINDOWS_EXPORTER
using PrometheusExporter.Instrumentation.DiskInfo;
#endif
#if WINDOWS_EXPORTER
using PrometheusExporter.Instrumentation.HardwareMonitor;
#endif
#if WINDOWS_EXPORTER
using PrometheusExporter.Instrumentation.HyperV;
#endif
#if WINDOWS_EXPORTER
using PrometheusExporter.Instrumentation.PerformanceCounter;
#endif
#if !WINDOWS_EXPORTER
using PrometheusExporter.Instrumentation.ProcessFileSystem;
#endif
using PrometheusExporter.Instrumentation.SensorOmron;
#if WINDOWS_EXPORTER
using PrometheusExporter.Instrumentation.SwitchBot;
#endif
#if !WINDOWS_EXPORTER
using PrometheusExporter.Instrumentation.SystemControl;
#endif
using PrometheusExporter.Instrumentation.Ping;
using PrometheusExporter.Instrumentation.WFWattch2;
#if WINDOWS_EXPORTER
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

#if WINDOWS_EXPORTER
    public bool EnableBle { get; set; }

    public BleOptions Ble { get; set; } = new();
#endif

    // BTWattch2

#if WINDOWS_EXPORTER
    public bool EnableBTWattch2 { get; set; }

    public BTWattch2Options BTWattch2 { get; set; } = new();
#endif

    // DiskInfo

#if WINDOWS_EXPORTER
    public bool EnableDiskInfo { get; set; }

    public DiskInfoOptions DiskInfo { get; set; } = new();
#endif

    // HardwareMonitor

#if WINDOWS_EXPORTER
    public bool EnableHardwareMonitor { get; set; }

    public HardwareMonitorOptions HardwareMonitor { get; set; } = new();
#endif

    // HardwareMonitor

#if WINDOWS_EXPORTER
    public bool EnableHyperV { get; set; }

    public HyperVOptions HyperV { get; set; } = new();
#endif

    // PerformanceCounter

#if WINDOWS_EXPORTER
    public bool EnablePerformanceCounter { get; set; }

    public PerformanceCounterOptions PerformanceCounter { get; set; } = new();
#endif

    // ProcessFileSystem

#if !WINDOWS_EXPORTER
    public bool EnableProcessFileSystem { get; set; }

    public ProcessFileSystemOptions ProcessFileSystem { get; set; } = new();
#endif

    // Ping

    public bool EnablePing { get; set; }

    public PingOptions Ping { get; set; } = new();

    // SensorOmron

    public bool EnableSensorOmron { get; set; }

    public SensorOmronOptions SensorOmron { get; set; } = new();

    // SwitchBot

#if WINDOWS_EXPORTER
    public bool EnableSwitchBot { get; set; }

    public SwitchBotOptions SwitchBot { get; set; } = new();
#endif

    // SystemControl

#if !WINDOWS_EXPORTER
    public bool EnableSystemControl { get; set; }

    public SystemControlOptions SystemControl { get; set; } = new();
#endif

    // WFWattch2

    public bool EnableWFWattch2 { get; set; }

    public WFWattch2Options WFWattch2 { get; set; } = new();

    // Wifi

#if WINDOWS_EXPORTER
    public bool EnableWifi { get; set; }

    public WifiOptions Wifi { get; set; } = new();
#endif
}
#pragma warning restore CA1819
