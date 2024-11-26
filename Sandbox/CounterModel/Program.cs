namespace CounterModel;

using System;
using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

internal static class Program
{
    public static async Task Main()
    {
        using var manager = new MetricsManager();
        using var plugin1 = new Plugin1(manager);
        using var plugin2 = new Plugin2(manager);

        while (true)
        {
            using var buffer = new PooledBufferWriter<byte>(65536);
            await manager.CollectAsync(buffer, default!).ConfigureAwait(false);

            var str = Encoding.UTF8.GetString(buffer.WrittenSpan);
            Debug.WriteLine(str);

            await Task.Delay(5000).ConfigureAwait(false);
        }
        // ReSharper disable once FunctionNeverReturns
    }
}

internal sealed class Plugin1 : IDisposable
{
    private readonly IGauge gauge2A;
    private readonly IGauge gauge2B;

    public Plugin1(MetricsManager manager)
    {
        var metrics = manager.CreateMetrics("plugin1_tagged");
        gauge2A = metrics.CreateGauge(new("host", "server"), new("test", "2a"));
        gauge2B = metrics.CreateGauge(new("host", "server"), new("test", "2b"));

        manager.AddBeforeCollectCallback(Callback);
    }

    public void Dispose()
    {
    }

    private void Callback()
    {
        gauge2A.Value = DateTime.Now.AddDays(-1).Ticks;
        gauge2B.Value = DateTime.Now.AddDays(1).Ticks;
    }
}

internal sealed class Plugin2 : IDisposable
{
    private readonly IMetrics metrics;
    private readonly List<IGauge> taggedGauges = [];

    private readonly Timer timer;

    private int counter;

    public Plugin2(MetricsManager manager)
    {
        metrics = manager.CreateMetrics("plugin2_tagged");

        timer = new Timer(Update, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(8000));
    }

    public void Dispose()
    {
        timer.Dispose();
    }

    private void Update(object? state)
    {
        counter++;

        foreach (var gauge in taggedGauges)
        {
            gauge.Remove();
        }
        taggedGauges.Clear();

        for (var i = 0; i < counter; i++)
        {
            var gauge = metrics.CreateGauge([new("name", $"no{i + 1}")]);
            taggedGauges.Add(gauge);
            gauge.Value = DateTime.Now.Second;
        }

        if (counter >= 3)
        {
            counter = 0;
        }
    }
}

// --------------------------------------------------------------------------------

internal sealed class MetricsManager : IDisposable
{
    private readonly List<IMetrics> gauges = [];

    private readonly SemaphoreSlim semaphore = new(1);

    private readonly List<Action> beforeCollectCallbacks = [];
    private readonly List<Func<CancellationToken, Task>> beforeCollectAsyncCallbacks = [];

    public void Dispose()
    {
        semaphore.Dispose();
    }

    public IMetrics CreateMetrics(string name)
    {
        var gauge = new Metrics(name);

        semaphore.Wait(0);
        try
        {
            gauges.Add(gauge);
        }
        finally
        {
            semaphore.Release();
        }

        return gauge;
    }

    public void AddBeforeCollectCallback(Action callback)
    {
        beforeCollectCallbacks.Add(callback);
    }

    public void AddBeforeCollectCallback(Func<CancellationToken, Task> callback)
    {
        beforeCollectAsyncCallbacks.Add(callback);
    }

    public async Task CollectAsync(IBufferWriter<byte> writer, CancellationToken cancel)
    {
        await semaphore.WaitAsync(0, cancel).ConfigureAwait(false);
        try
        {
            foreach (var callback in beforeCollectCallbacks)
            {
                callback();
            }

            await Task.WhenAll(beforeCollectAsyncCallbacks.Select(callback => callback(cancel))).ConfigureAwait(false);

            foreach (var gauge in gauges)
            {
                gauge.Write(writer);
            }

            Helper.WriteEof(writer);
        }
        finally
        {
            semaphore.Release();
        }
    }
}

// --------------------------------------------------------------------------------

internal interface IMetrics
{
    IGauge CreateGauge(params KeyValuePair<string, object?>[] tags);

    void Write(IBufferWriter<byte> writer);
}

internal interface IGauge
{
    double Value { get; set; }

    void Remove();
}

// --------------------------------------------------------------------------------

internal sealed class Metrics : IMetrics
{
    private readonly string name;

    private readonly object sync = new();

    private readonly List<Gauge> entries = [];

    public Metrics(string name)
    {
        this.name = name;
    }

    void IMetrics.Write(IBufferWriter<byte> writer)
    {
        Helper.WriteType(writer, name);

        lock (sync)
        {
            foreach (var entry in entries)
            {
                entry.Write(writer, name);
            }
        }
    }

    public IGauge CreateGauge(params KeyValuePair<string, object?>[] tags)
    {
        lock (sync)
        {
            var gauge = new Gauge(this, tags);
            entries.Add(gauge);
            return gauge;
        }
    }

    internal void Unregister(Gauge entry)
    {
        lock (sync)
        {
            entries.Remove(entry);
        }
    }
}

internal sealed class Gauge : IGauge
{
    private readonly Metrics parent;

    private readonly Tag[] tags;

    public double Value
    {
        get => Interlocked.Exchange(ref field, field);
        set => Interlocked.Exchange(ref field, value);
    }

    public Gauge(Metrics parent, KeyValuePair<string, object?>[] tags)
    {
        this.parent = parent;
        this.tags = Helper.PrepareTags(tags);
    }

    public void Remove()
    {
        parent.Unregister(this);
    }

    internal void Write(IBufferWriter<byte> writer, string name)
    {
        Helper.WriteValue(writer, name, Value, tags);
    }
}

// --------------------------------------------------------------------------------

internal record Tag(string Key, string Value);

// --------------------------------------------------------------------------------

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
