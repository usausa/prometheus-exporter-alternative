namespace PrometheusExporter.Instrumentation.Npu.Windows;

internal sealed class NpuOptions
{
    public int Interval { get; set; } = 1000;

    public bool ContextTime { get; set; }

    public string? Luid { get; set; }
}
