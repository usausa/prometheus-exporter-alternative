namespace PrometheusExporter.Instrumentation.Linux;

internal sealed class LinuxOptions
{
    public int UpdateDuration { get; set; } = 1000;

    public bool Uptime { get; set; } = true;

    public bool System { get; set; } = true;

    public bool LoadAverage { get; set; } = true;

    public string[] Memory { get; set; } = ["*"];

    public string[] VirtualMemory { get; set; } = ["*"];

    public bool Mount { get; set; } = true;

    public bool Disk { get; set; } = true;

    public bool FileDescriptor { get; set; } = true;

    public bool Network { get; set; } = true;

    public bool Tcp { get; set; } = true;

    public bool Tcp6 { get; set; } = true;

    public bool Wireless { get; set; } = true;

    public bool ProcessSummary { get; set; } = true;

    public bool Cpu { get; set; } = true;

    public bool Battery { get; set; } = true;

    public bool Mains { get; set; } = true;

    public bool HardwareMonitor { get; set; } = true;
}
