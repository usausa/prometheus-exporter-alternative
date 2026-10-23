namespace PrometheusExporter.Infrastructure.Windows.Pdh;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
internal readonly struct PdhCounterItem
{
    public readonly nint NamePointer;

    public readonly uint Status;

    public readonly double Value;
}

public sealed class PdhCounterArray
{
    private const uint Format = NativeMethods.PdhFmtDouble | NativeMethods.PdhFmtNoCap100;

    private readonly nint counter;

    private byte[] buffer;

    private uint bufferSize;

    internal PdhCounterArray(string path, nint counter)
    {
        Path = path;
        this.counter = counter;
        buffer = [];
    }

    public string Path { get; }

    public int Count { get; private set; }

    private ReadOnlySpan<PdhCounterItem> Items =>
        MemoryMarshal.Cast<byte, PdhCounterItem>(buffer.AsSpan(0, Count * Unsafe.SizeOf<PdhCounterItem>()));

    public ReadOnlySpan<char> GetName(int index)
    {
        unsafe
        {
            return MemoryMarshal.CreateReadOnlySpanFromNullTerminated((char*)Items[index].NamePointer);
        }
    }

    public double GetValue(int index) => Items[index].Value;

    public bool HasValue(int index)
    {
        var status = Items[index].Status;
        return (status == NativeMethods.PdhCStatusValidData) || (status == NativeMethods.PdhCStatusNewData);
    }

    internal bool Read()
    {
        Count = 0;

        var size = bufferSize;
        var status = ReadCore(ref size, out var count);
        if (status == NativeMethods.PdhMoreData)
        {
            buffer = new byte[size];
            bufferSize = size;
            status = ReadCore(ref size, out count);
        }

        if (status != 0)
        {
            return false;
        }

        Count = (int)count;
        return true;
    }

    private uint ReadCore(ref uint size, out uint count)
    {
        fixed (byte* pointer = buffer)
        {
            return NativeMethods.PdhGetFormattedCounterArrayW(counter, Format, ref size, out count, (nint)pointer);
        }
    }
}
