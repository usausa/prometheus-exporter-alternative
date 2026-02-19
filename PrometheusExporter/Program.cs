using System.Runtime.InteropServices;

using PrometheusExporter;
using PrometheusExporter.Exporter;
using PrometheusExporter.Instrumentation;
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
builder.Services.AddMetricManager();

// Instrumentation
builder.Services.AddInstrumentation(builder.Configuration, config =>
{
    config.Host = setting.Host;
    config.Enable = setting.Enable;
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
#pragma warning disable CA1873
log.InfoServiceSettingsEnvironment(typeof(Program).Assembly.GetName().Version, Environment.CurrentDirectory);
#pragma warning restore CA1873

host.Run();
