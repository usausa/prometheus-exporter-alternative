namespace PrometheusExporter.Instrumentation.ProcessFileSystem;

public sealed class ProcessFileSystemOptions
{
    public string Host { get; set; } = default!;

    public int UpdateDuration { get; set; } = 1000;
}
