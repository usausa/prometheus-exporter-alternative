namespace PrometheusExporter.Instrumentation.Mac;

internal sealed class MacOptions
{
    public int UpdateDuration { get; set; } = 1000;

    public bool Uptime { get; set; } = true;

    public bool Cpu { get; set; } = true;

    public bool LoadAverage { get; set; } = true;

    public bool Memory { get; set; } = true;

    public bool SwapUsage { get; set; } = true;

    public bool FileSystem { get; set; } = true;

    public bool Disk { get; set; } = true;

    public bool FileDescriptor { get; set; } = true;

    public bool Network { get; set; } = true;

    public bool ProcessSummary { get; set; } = true;

    public bool CpuFrequency { get; set; } = true;

    public bool Gpu { get; set; } = true;

    public bool Power { get; set; } = true;

    public bool HardwareMonitor { get; set; } = true;
}
