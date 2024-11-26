namespace PrometheusExporter.Metrics;

using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;

internal static class Helper
{
    private const byte Blank = (byte)' ';
    private const byte LineFeed = (byte)'\n';
    private const byte TagStart = (byte)'{';
    private const byte TagEnd = (byte)'}';
    private const byte Comma = (byte)',';
    private const byte Equal = (byte)'=';
    private const byte Quote = (byte)'"';
    private const byte Solidus = (byte)'\\';

    public static Tag[] PrepareTags(KeyValuePair<string, object?>[] tags)
    {
        var values = new Tag[tags.Length];

        for (var i = 0; i < tags.Length; i++)
        {
            var tag = tags[i];
            values[i] = new Tag(tag.Key, GetValueString(tag.Value));
        }

        return values;

        static string GetValueString(object? value)
        {
            if (value is bool b)
            {
                return b ? "true" : "false";
            }

            return value?.ToString() ?? string.Empty;
        }
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
    private static void WriteString(IBufferWriter<byte> writer, string value)
    {
        var span = writer.GetSpan(value.Length * 3);

        var written = 0;
        foreach (var c in value)
        {
            var ordinal = (ushort)c;
            written += WriteUnicodeNoEscape(span[written..], ordinal);
        }

        writer.Advance(written);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteEscapedString(IBufferWriter<byte> writer, string value)
    {
        var span = writer.GetSpan(value.Length * 3);

        var written = 0;
        foreach (var c in value)
        {
            var ordinal = (ushort)c;
            switch (ordinal)
            {
                case Quote:
                    span[written++] = Solidus;
                    span[written++] = Quote;
                    break;
                case Solidus:
                    span[written++] = Solidus;
                    span[written++] = Solidus;
                    break;
                case LineFeed:
                    span[written++] = Solidus;
                    span[written++] = unchecked((byte)'n');
                    break;
                default:
                    written += WriteUnicodeNoEscape(span[written..], ordinal);
                    break;
            }
        }

        writer.Advance(written);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteUnicodeNoEscape(Span<byte> span, ushort ordinal)
    {
        var written = 0;
        if (ordinal <= 0x7F)
        {
            span[written++] = unchecked((byte)ordinal);
        }
        else if (ordinal <= 0x07FF)
        {
            span[written++] = unchecked((byte)(0b_1100_0000 | (ordinal >> 6)));
            span[written++] = unchecked((byte)(0b_1000_0000 | (ordinal & 0b_0011_1111)));
        }
        else
        {
            span[written++] = unchecked((byte)(0b_1110_0000 | (ordinal >> 12)));
            span[written++] = unchecked((byte)(0b_1000_0000 | ((ordinal >> 6) & 0b_0011_1111)));
            span[written++] = unchecked((byte)(0b_1000_0000 | (ordinal & 0b_0011_1111)));
        }

        return written;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteType(IBufferWriter<byte> writer, string name)
    {
        WriteBytes(writer, "# TYPE"u8);
        WriteByte(writer, Blank);
        WriteString(writer, name);
        WriteByte(writer, Blank);
        WriteBytes(writer, "gauge"u8);
        WriteByte(writer, LineFeed);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteEof(IBufferWriter<byte> writer)
    {
        WriteBytes(writer, "# EOF"u8);
        WriteByte(writer, LineFeed);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteValue(IBufferWriter<byte> writer, string name, double value, Tag[] tags)
    {
        WriteString(writer, name);
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
                WriteString(writer, tag.Key);
                WriteByte(writer, Equal);
                WriteByte(writer, Quote);
                WriteEscapedString(writer, tag.Value);
                WriteByte(writer, Quote);
            }
            WriteByte(writer, TagEnd);
        }
        WriteByte(writer, Blank);
        WriteDouble(writer, value);
        WriteByte(writer, LineFeed);
    }
}
