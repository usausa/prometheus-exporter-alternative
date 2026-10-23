namespace PrometheusExporter.Instrumentation.Npu.Windows;

using System.Globalization;

internal sealed record NpuDevice(
    string Luid,
    uint PhysicalId,
    string Description);

internal static class NpuDeviceLocator
{
    private static readonly Guid ComputeAcceleratorClass = new("f01a9d53-3ff6-48d2-9f97-c8a7004be10c");

    private static readonly DevPropKey GpuLuidKey = MakeKey("60b193cb-5276-4d0f-96fc-f173abad3ec6", 2);

    private static readonly DevPropKey GpuPhysicalAdapterIndexKey = MakeKey("60b193cb-5276-4d0f-96fc-f173abad3ec6", 3);

    private static readonly DevPropKey NameKey = MakeKey("b725f130-47ef-101a-a5f1-02608c9eebac", 10);

    private static readonly nint InvalidHandle = -1;

    public static List<NpuDevice> Locate()
    {
        var devices = new List<NpuDevice>();

        var classGuid = ComputeAcceleratorClass;
        var handle = NativeMethods.SetupDiGetClassDevsW(ref classGuid, nint.Zero, nint.Zero, NativeMethods.DigcfPresent);
        if (handle == InvalidHandle)
        {
            return devices;
        }

        try
        {
            var info = default(SpDevInfoData);
            info.Size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<SpDevInfoData>();

            for (var index = 0u; NativeMethods.SetupDiEnumDeviceInfo(handle, index, ref info); index++)
            {
                if (!TryGetUInt64(handle, ref info, GpuLuidKey, out var luid))
                {
                    continue;
                }

                _ = TryGetUInt32(handle, ref info, GpuPhysicalAdapterIndexKey, out var physicalId);
                _ = TryGetString(handle, ref info, NameKey, out var description);

                devices.Add(new NpuDevice(FormatLuid(luid), physicalId, description));
            }
        }
        finally
        {
            _ = NativeMethods.SetupDiDestroyDeviceInfoList(handle);
        }

        return devices;
    }

    public static string FormatLuid(ulong value) =>
        string.Create(CultureInfo.InvariantCulture, $"0x{(uint)(value >> 32):X8}_0x{(uint)(value & 0xFFFFFFFF):X8}");

    private static DevPropKey MakeKey(string formatId, uint propertyId) =>
        new() { FormatId = new Guid(formatId), PropertyId = propertyId };

    private static bool TryGetProperty(nint deviceInfoSet, ref SpDevInfoData info, DevPropKey key, uint expectedType, out byte[] buffer)
    {
        buffer = [];

        _ = NativeMethods.SetupDiGetDevicePropertyW(deviceInfoSet, ref info, ref key, out _, nint.Zero, 0, out var requiredSize, 0);
        if (requiredSize == 0)
        {
            return false;
        }

        var data = new byte[requiredSize];

        fixed (byte* pointer = data)
        {
            if (!NativeMethods.SetupDiGetDevicePropertyW(deviceInfoSet, ref info, ref key, out var propertyType, (nint)pointer, requiredSize, out _, 0) ||
                (propertyType != expectedType))
            {
                return false;
            }
        }

        buffer = data;
        return true;
    }

    private static bool TryGetUInt64(nint deviceInfoSet, ref SpDevInfoData info, DevPropKey key, out ulong value)
    {
        value = 0;

        if (!TryGetProperty(deviceInfoSet, ref info, key, NativeMethods.DevPropTypeUint64, out var buffer) || (buffer.Length < sizeof(ulong)))
        {
            return false;
        }

        value = BitConverter.ToUInt64(buffer, 0);
        return true;
    }

    private static bool TryGetUInt32(nint deviceInfoSet, ref SpDevInfoData info, DevPropKey key, out uint value)
    {
        value = 0;

        if (!TryGetProperty(deviceInfoSet, ref info, key, NativeMethods.DevPropTypeUint32, out var buffer) || (buffer.Length < sizeof(uint)))
        {
            return false;
        }

        value = BitConverter.ToUInt32(buffer, 0);
        return true;
    }

    private static bool TryGetString(nint deviceInfoSet, ref SpDevInfoData info, DevPropKey key, out string value)
    {
        value = string.Empty;

        if (!TryGetProperty(deviceInfoSet, ref info, key, NativeMethods.DevPropTypeString, out var buffer))
        {
            return false;
        }

        value = System.Text.Encoding.Unicode.GetString(buffer).TrimEnd('\0');
        return true;
    }
}
