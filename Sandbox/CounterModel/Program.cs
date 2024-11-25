namespace CounterModel;

using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Text;

internal static class Program
{
    public static void Main()
    {
        using var manager = new CounterManager();

    }
}

internal sealed class Plugin1 : IDisposable
{
    // TODO コールバックで値を設定、単体とタグあり、タグは変動
    public void Dispose()
    {
        // TODO
    }
}

internal sealed class Plugin2
{
    // TODO タイマーで定期的に設定、単体とタグあり、タグは変動
}

// --------------------------------------------------------------------------------

internal sealed class CounterManager : IDisposable
{
    private readonly List<IWriter> counters = [];

    private readonly SemaphoreSlim semaphore = new(1);

    private readonly List<Action> beforeCollectCallbacks = [];
    private readonly List<Func<CancellationToken, Task>> beforeCollectAsyncCallbacks = [];

    public void Dispose()
    {
        semaphore.Dispose();
    }

    public ICounter<T> CreateCounter<T>(string name)
        where T : struct
    {
        var counter = new Counter<T>(name);

        semaphore.Wait(0);
        try
        {
            counters.Add(counter);
        }
        finally
        {
            semaphore.Release();
        }

        return counter;
    }

    public ITaggedCounter<T> CreateTaggedCounter<T>(string name)
        where T : struct
    {
        var counter = new TaggedCounter<T>(name);

        semaphore.Wait(0);
        try
        {
            counters.Add(counter);
        }
        finally
        {
            semaphore.Release();
        }

        return counter;
    }

    public void AddBeforeCollectCallback(Action callback)
    {
        beforeCollectCallbacks.Add(callback);
    }

    public void AddBeforeCollectCallback(Func<CancellationToken, Task> callback)
    {
        beforeCollectAsyncCallbacks.Add(callback);
    }

    public async Task Collect(IBufferWriter<byte> writer, CancellationToken cancel)
    {
        await semaphore.WaitAsync(0, cancel).ConfigureAwait(false);
        try
        {
            foreach (var callback in beforeCollectCallbacks)
            {
                callback();
            }

            await Task.WhenAll(beforeCollectAsyncCallbacks.Select((callback) => callback(cancel))).ConfigureAwait(false);

            // TODO Write
            foreach (var counter in counters)
            {
                counter.Write(writer);
            }

            // TODO EOF
        }
        finally
        {
            semaphore.Release();
        }
    }
}

// --------------------------------------------------------------------------------

internal interface IWriter
{
    void Write(IBufferWriter<byte> writer);
}

internal interface ICounter<T>
    where T : struct
{
    T? Current { get; set; }
}

internal interface ITaggedCounter<T>
    where T : struct
{
    ICounter<T> CreateCounter(params KeyValuePair<string, string>[] tags);
}

// --------------------------------------------------------------------------------

internal sealed class Counter<T> : ICounter<T>, IWriter
    where T : struct
{
    private readonly string name;

    private readonly object sync = new();

    public T? Current
    {
        get
        {
            lock (sync)
            {
                return field;
            }
        }
        set
        {
            lock (sync)
            {
                field = value;
            }
        }
    }

    public Counter(string name)
    {
        this.name = name;
    }

    void IWriter.Write(IBufferWriter<byte> writer)
    {
        var value = Current;
        if (value.HasValue)
        {
            // TODO
            //writer.
        }
    }
}

internal sealed class TaggedCounter<T> : ITaggedCounter<T>, IWriter
    where T : struct
{
    private readonly string name;

    private readonly object sync = new();

    private readonly List<TaggedCounterEntry<T>> entries = [];

    public TaggedCounter(string name)
    {
        this.name = name;
    }

    // TODO
    void IWriter.Write(IBufferWriter<byte> writer) => throw new NotImplementedException();

    // TODO Create with tag dup check?
    public ICounter<T> CreateCounter(params KeyValuePair<string, string>[] tags) => throw new NotImplementedException();

    internal void Unregister(TaggedCounterEntry<T> entry)
    {
        lock (sync)
        {
            entries.Remove(entry);
        }
    }
}

internal sealed class TaggedCounterEntry<T> : IDisposable
    where T : struct
{
    private readonly TaggedCounter<T> parent;

    private readonly object sync = new();

    public T? Current
    {
        get
        {
            lock (sync)
            {
                return field;
            }
        }
        set
        {
            lock (sync)
            {
                field = value;
            }
        }
    }

    public TaggedCounterEntry(TaggedCounter<T> parent)
    {
        this.parent = parent;
    }

    public void Dispose()
    {
        parent.Unregister(this);
    }
}

// --------------------------------------------------------------------------------

// TODO Operator
internal static class ValueWriter
{
    //internal interface IValueWriter<T>
    //    where T : struct
    //{
    //    void Write(IBufferWriter<byte> writer, T value);
    //}

    //internal static class Writer<T>
    //{
    //}

    // TODO
    // long, int
    // ulong, uint
    // double+f, float+f
    //var buffer = new byte[128];
    //Utf8Formatter.TryFormat(1.234d, buffer, out var written, StandardFormat.Parse("F2"));
    //Debug.WriteLine(Encoding.UTF8.GetString(buffer.AsSpan(0, written)));
}
