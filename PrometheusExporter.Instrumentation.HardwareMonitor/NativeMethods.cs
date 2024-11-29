namespace PrometheusExporter.Instrumentation.HardwareMonitor;

using System.Runtime.InteropServices;

internal static class NativeMethods
{
    [DllImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetPhysicallyInstalledSystemMemory(out long totalMemoryInKilobytes);
}
