namespace PrometheusExporter.Metrics;

using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

internal static class Helper
{
    private const byte Blank = (byte)' ';
    private const byte LineFeed = (byte)'\n';
    private const byte TagStart = (byte)'{';
    private const byte TagEnd = (byte)'}';
    private const byte Comma = (byte)',';
    private const byte Equal = (byte)'=';
    private const byte Quote = (byte)'"';
    private const byte BackSlash = (byte)'\\';

    public static Tag[] ConvertTags(KeyValuePair<string, object?>[] tags)
    {
        var values = new Tag[tags.Length];

        for (var i = 0; i < tags.Length; i++)
        {
            var tag = tags[i];
            var value = GetValueString(tag.Value);
            values[i] = new Tag(tag.Key, value);
        }

        return values;
    }

    private static string GetValueString(object? value)
    {
        if (value is bool b)
        {
            return b ? "true" : "false";
        }

        var str = value?.ToString();
        if (String.IsNullOrEmpty(str))
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var c in str)
        {
            var ordinal = (ushort)c;
            switch (ordinal)
            {
                case Quote:
                    sb.Append(BackSlash);
                    sb.Append(Quote);
                    break;
                case BackSlash:
                    sb.Append(BackSlash);
                    sb.Append(BackSlash);
                    break;
                case LineFeed:
                    sb.Append(BackSlash);
                    sb.Append((byte)'n');
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }

        return sb.ToString();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteByte(IBufferWriter<byte> writer, byte value)
    {
        var span = writer.GetSpan(1);
        span[0] = value;
        writer.Advance(1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteBytes(IBufferWriter<byte> writer, ReadOnlySpan<byte> value)
    {
        var span = writer.GetSpan(value.Length);
        value.CopyTo(span);
        writer.Advance(value.Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteDouble(IBufferWriter<byte> writer, double value)
    {
        if (Double.IsFinite(value))
        {
            Span<char> buffer = stackalloc char[128];
            value.TryFormat(buffer, out var written, "G", CultureInfo.InvariantCulture);

            var span = writer.GetSpan(written);
            for (var i = 0; i < written; i++)
            {
                span[i] = unchecked((byte)buffer[i]);
            }

            writer.Advance(written);
        }
        else if (Double.IsPositiveInfinity(value))
        {
            WriteBytes(writer, "+Inf"u8);
        }
        else if (Double.IsNegativeInfinity(value))
        {
            WriteBytes(writer, "-Inf"u8);
        }
        else
        {
            WriteBytes(writer, "Nan"u8);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteLong(IBufferWriter<byte> writer, long value)
    {
        Span<char> buffer = stackalloc char[20];
        value.TryFormat(buffer, out var written, "G", CultureInfo.InvariantCulture);

        var span = writer.GetSpan(written);
        for (var i = 0; i < written; i++)
        {
            span[i] = unchecked((byte)buffer[i]);
        }

        writer.Advance(written);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteType(IBufferWriter<byte> writer, byte[] type, byte[] name)
    {
        WriteBytes(writer, "# TYPE"u8);
        WriteByte(writer, Blank);
        WriteBytes(writer, name);
        WriteByte(writer, Blank);
        WriteBytes(writer, type);
        WriteByte(writer, LineFeed);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteEof(IBufferWriter<byte> writer)
    {
        WriteBytes(writer, "# EOF"u8);
        WriteByte(writer, LineFeed);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteValue(IBufferWriter<byte> writer, long timestamp, byte[] name, double value, Tag[] tags)
    {
        WriteBytes(writer, name);
        if (tags.Length > 0)
        {
            WriteByte(writer, TagStart);
            for (var i = 0; i < tags.Length; i++)
            {
                if (i > 0)
                {
                    WriteByte(writer, Comma);
                }

                var tag = tags[i];
                WriteBytes(writer, tag.KeyBytes);
                WriteByte(writer, Equal);
                WriteByte(writer, Quote);
                WriteBytes(writer, tag.ValueBytes);
                WriteByte(writer, Quote);
            }
            WriteByte(writer, TagEnd);
        }
        WriteByte(writer, Blank);
        WriteDouble(writer, value);
        WriteByte(writer, Blank);
        WriteLong(writer, timestamp);
        WriteByte(writer, LineFeed);
    }
}
