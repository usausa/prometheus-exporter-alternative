namespace PrometheusExporter.Instrumentation.PerformanceCounter;

using System.Runtime.InteropServices;

internal static partial class NativeMethods
{
    public const uint PdhFmtDouble = 0x00000200;

    // Without this flag PDH clamps the formatted value at 100, which breaks a sum of several instances
    public const uint PdhFmtNoCap100 = 0x00008000;

    public const uint PdhCStatusValidData = 0x00000000;

    public const uint PdhCStatusNewData = 0x00000001;

    public const uint PdhMoreData = 0x800007D2;

    [LibraryImport("pdh.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static partial uint PdhOpenQueryW(nint dataSource, nint userData, out nint query);

    [LibraryImport("pdh.dll", StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static partial uint PdhAddCounterW(nint query, string fullCounterPath, nint userData, out nint counter);

    [LibraryImport("pdh.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static partial uint PdhCollectQueryData(nint query);

    [LibraryImport("pdh.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static partial uint PdhGetFormattedCounterArrayW(nint counter, uint format, ref uint bufferSize, out uint itemCount, nint itemBuffer);

    [LibraryImport("pdh.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static partial uint PdhCloseQuery(nint query);
}
