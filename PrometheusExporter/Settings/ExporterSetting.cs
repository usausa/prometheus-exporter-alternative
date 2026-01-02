namespace PrometheusExporter.Settings;

#pragma warning disable CA1819
public sealed class ExporterSetting
{
    // Application

    public string EndPoint { get; set; } = default!;

    public string? Host { get; set; }

    public string[] Enable { get; set; } = default!;
}
#pragma warning restore CA1819
