using System.Runtime.InteropServices;

using PrometheusExporter;
using PrometheusExporter.Exporter;
using PrometheusExporter.Instrumentation;
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
using PrometheusExporter.Instrumentation.Ping;
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
using PrometheusExporter.Instrumentation.WFWattch2;
#if WINDOWS_EXPORTER
using PrometheusExporter.Instrumentation.Wifi;
#endif
using PrometheusExporter.Metrics;
using PrometheusExporter.Settings;

using Serilog;

// Builder
Directory.SetCurrentDirectory(AppContext.BaseDirectory);
var builder = Host.CreateApplicationBuilder(args);

// Setting
var setting = builder.Configuration.GetSection("Exporter").Get<ExporterSetting>()!;

// Service
builder.Services
    .AddWindowsService()
    .AddSystemd();

// Logging
builder.Logging.ClearProviders();
builder.Services.AddSerilog(options => options.ReadFrom.Configuration(builder.Configuration));

// Metrics
builder.Services.AddPrometheusMetrics((metrics, _) =>
{
    var host = setting.Host ?? Environment.MachineName;

    if (setting.EnableApplication)
    {
        metrics.AddApplicationInstrumentation(new ApplicationOptions { Host = host });
    }

    // System

#if WINDOWS_EXPORTER
    if (setting.EnableHardwareMonitor)
    {
        setting.HardwareMonitor.Host = String.IsNullOrWhiteSpace(setting.HardwareMonitor.Host) ? host : setting.HardwareMonitor.Host;
        metrics.AddHardwareMonitorInstrumentation(setting.HardwareMonitor);
    }
#endif
#if WINDOWS_EXPORTER
    if (setting.EnablePerformanceCounter)
    {
        setting.PerformanceCounter.Host = String.IsNullOrWhiteSpace(setting.PerformanceCounter.Host) ? host : setting.PerformanceCounter.Host;
        metrics.AddPerformanceCounterInstrumentation(setting.PerformanceCounter);
    }
#endif
#if WINDOWS_EXPORTER
    if (setting.EnableDiskInfo)
    {
        setting.DiskInfo.Host = String.IsNullOrWhiteSpace(setting.DiskInfo.Host) ? host : setting.DiskInfo.Host;
        metrics.AddDiskInfoInstrumentation(setting.DiskInfo);
    }
#endif
#if !WINDOWS_EXPORTER
    if (setting.EnableProcessFileSystem)
    {
        setting.ProcessFileSystem.Host = String.IsNullOrWhiteSpace(setting.ProcessFileSystem.Host) ? host : setting.ProcessFileSystem.Host;
        metrics.AddProcessFileSystemInstrumentation(setting.ProcessFileSystem);
    }
#endif
#if !WINDOWS_EXPORTER
    if (setting.EnableSystemControl)
    {
        setting.SystemControl.Host = String.IsNullOrWhiteSpace(setting.SystemControl.Host) ? host : setting.SystemControl.Host;
        metrics.AddSystemControlInstrumentation(setting.SystemControl);
    }
#endif

    // VirtualMachine

#if WINDOWS_EXPORTER
    if (setting.EnableHyperV)
    {
        setting.HyperV.Host = String.IsNullOrWhiteSpace(setting.HyperV.Host) ? host : setting.HyperV.Host;
        metrics.AddHyperVInstrumentation(setting.HyperV);
    }
#endif

    // Sensor

#if WINDOWS_EXPORTER
    if (setting.EnableBTWattch2)
    {
        metrics.AddBTWattch2Instrumentation(setting.BTWattch2);
    }
#endif
    if (setting.EnableWFWattch2)
    {
        metrics.AddWFWattch2Instrumentation(setting.WFWattch2);
    }
#if WINDOWS_EXPORTER
    if (setting.EnableSwitchBot)
    {
        metrics.AddSwitchBotInstrumentation(setting.SwitchBot);
    }
#endif
    if (setting.EnableSensorOmron)
    {
        metrics.AddSensorOmronInstrumentation(setting.SensorOmron);
    }

    // Network

#if WINDOWS_EXPORTER
    if (setting.EnableBle)
    {
        setting.Ble.Host = String.IsNullOrWhiteSpace(setting.Ble.Host) ? host : setting.Ble.Host;
        metrics.AddBleInstrumentation(setting.Ble);
    }
#endif
#if WINDOWS_EXPORTER
    if (setting.EnableWifi)
    {
        setting.Wifi.Host = String.IsNullOrWhiteSpace(setting.Wifi.Host) ? host : setting.Wifi.Host;
        metrics.AddWifiInstrumentation(setting.Wifi);
    }
#endif
    if (setting.EnablePing)
    {
        setting.Ping.Host = String.IsNullOrWhiteSpace(setting.Ping.Host) ? host : setting.Ping.Host;
        metrics.AddPingInstrumentation(setting.Ping);
    }
});

// Worker
builder.Services.AddSingleton(new ExporterWorkerOptions
{
    EndPoint = setting.EndPoint
});
builder.Services.AddHostedService<ExporterWorker>();

// Build
var host = builder.Build();

// Startup
var log = host.Services.GetRequiredService<ILogger<Program>>();
log.InfoServiceStart();
log.InfoServiceSettingsRuntime(RuntimeInformation.OSDescription, RuntimeInformation.FrameworkDescription, RuntimeInformation.RuntimeIdentifier);
log.InfoServiceSettingsEnvironment(typeof(Program).Assembly.GetName().Version, Environment.CurrentDirectory);

host.Run();
