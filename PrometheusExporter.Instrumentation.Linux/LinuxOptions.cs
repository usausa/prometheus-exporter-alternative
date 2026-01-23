namespace PrometheusExporter.Instrumentation.Linux;

internal sealed class LinuxOptions
{
    public int UpdateDuration { get; set; } = 1000;

    public bool Uptime { get; set; } = true;

    public bool Statics { get; set; } = true;

    public bool LoadAverage { get; set; } = true;

    public string[] Memory { get; set; } = ["*"];

    public string[] VirtualMemory { get; set; } = ["*"];

    public bool Partition { get; set; } = true;

    public bool DiskStatics { get; set; } = true;

    public bool FileDescriptor { get; set; } = true;

    public bool NetworkStatic { get; set; } = true;

    public bool TcpStatic { get; set; } = true;

    public bool Tcp6Static { get; set; } = true;

    public bool ProcessSummary { get; set; } = true;

    public bool Cpu { get; set; } = true;

    public bool Battery { get; set; } = true;

    public bool MainsAdapter { get; set; } = true;

    public bool HardwareMonitor { get; set; } = true;
}
