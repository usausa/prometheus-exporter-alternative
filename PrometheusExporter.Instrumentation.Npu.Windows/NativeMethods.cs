namespace PrometheusExporter.Instrumentation.Npu.Windows;

using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
internal struct SpDevInfoData
{
    public uint Size;

    public Guid ClassGuid;

    public uint DevInst;

    public nint Reserved;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DevPropKey
{
    public Guid FormatId;

    public uint PropertyId;
}

internal static partial class NativeMethods
{
    public const uint DigcfPresent = 0x00000002;

    public const uint DevPropTypeUint32 = 0x00000007;

    public const uint DevPropTypeUint64 = 0x00000009;

    public const uint DevPropTypeString = 0x00000012;

    [LibraryImport("setupapi.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static partial nint SetupDiGetClassDevsW(ref Guid classGuid, nint enumerator, nint parent, uint flags);

    [LibraryImport("setupapi.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetupDiEnumDeviceInfo(nint deviceInfoSet, uint memberIndex, ref SpDevInfoData deviceInfoData);

    [LibraryImport("setupapi.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetupDiGetDevicePropertyW(nint deviceInfoSet, ref SpDevInfoData deviceInfoData, ref DevPropKey propertyKey, out uint propertyType, nint propertyBuffer, uint propertyBufferSize, out uint requiredSize, uint flags);

    [LibraryImport("setupapi.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetupDiDestroyDeviceInfoList(nint deviceInfoSet);
}
