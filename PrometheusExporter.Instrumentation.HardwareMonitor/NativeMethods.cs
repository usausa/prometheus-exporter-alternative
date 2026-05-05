namespace PrometheusExporter.Instrumentation.HardwareMonitor;

using System.Runtime.InteropServices;

internal static partial class NativeMethods
{
    [LibraryImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetPhysicallyInstalledSystemMemory(out long totalMemoryInKilobytes);
}
