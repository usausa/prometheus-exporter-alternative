namespace PrometheusExporter.Instrumentation.Linux;

internal sealed class LinuxOptions
{
    public int UpdateDuration { get; set; } = 1000;

    public bool Uptime { get; set; } = true;

    public bool SystemStat { get; set; } = true;

    public bool LoadAverage { get; set; } = true;

    public string[] Memory { get; set; } = ["*"];

    public string[] VirtualMemory { get; set; } = ["*"];

    public bool Mount { get; set; } = true;

    public bool DiskStat { get; set; } = true;

    public bool FileDescriptor { get; set; } = true;

    public bool NetworkStat { get; set; } = true;

    public bool TcpStat { get; set; } = true;

    public bool Tcp6Stat { get; set; } = true;

    public bool WirelessStat { get; set; } = true;

    public bool ProcessSummary { get; set; } = true;

    public bool Cpu { get; set; } = true;

    public bool Battery { get; set; } = true;

    public bool Mains { get; set; } = true;

    public bool HardwareMonitor { get; set; } = true;
}
