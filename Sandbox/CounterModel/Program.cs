namespace CounterModel;

using System.Buffers;

internal static class Program
{
    public static void Main()
    {
        using var manager = new CounterManager("Test");
    }
}

internal sealed class Plugin1
{
    // TODO
}

internal sealed class Plugin2
{
    // TODO
}

// --------------------------------------------------------------------------------

internal sealed class CounterManager : IDisposable
{
    internal string Name { get; set; }

    private readonly List<ICounter> counters = [];

    private readonly SemaphoreSlim semaphore = new(1);

    private readonly List<Action> beforeCollectCallbacks = [];
    private readonly List<Func<CancellationToken, Task>> beforeCollectAsyncCallbacks = [];

    public CounterManager(string name)
    {
        Name = name;
    }

    public void Dispose()
    {
        semaphore.Dispose();
    }

    public ICounter CreateCounter<T>(string name)
        where T : struct
    {
        var counter = new Counter<T>(this, name);

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

// TODO wrap & benchmark?
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
    // long, int, short,
    // byte, bool
    // ulong, uint, ushort
    // double+f, float+f
}

// --------------------------------------------------------------------------------

internal interface ICounter
{
    void Write(IBufferWriter<byte> writer);
}

internal interface ITaggedICounter : ICounter
{
    // TODO
}

// --------------------------------------------------------------------------------

internal sealed class Counter<T> : ICounter
    where T : struct
{
    private readonly CounterManager manager;

    // TODO interlocked or not supported, wrap & benchmark?
    public T? Value { get; set; }

    public Counter(CounterManager manager, string name)
    {
        this.manager = manager;
    }

    // TODO
    public void Write(IBufferWriter<byte> writer) => throw new NotImplementedException();
}

internal sealed class TaggedCounter<T> : ITaggedICounter
    where T : struct
{
    private readonly CounterManager manager;

    public TaggedCounter(CounterManager manager, string name)
    {
        this.manager = manager;
    }

    // TODO
    public void Write(IBufferWriter<byte> writer) => throw new NotImplementedException();

    internal void Unregister(TaggedCounterEntry<T> entry)
    {
        // TODO
    }
}

internal sealed class TaggedCounterEntry<T> : IDisposable
    where T : struct
{
    private readonly TaggedCounter<T> parent;

    // TODO
    public T? Value { get; set; }

    public TaggedCounterEntry(TaggedCounter<T> parent)
    {
        this.parent = parent;
    }

    public void Dispose()
    {
        parent.Unregister(this);
    }
}
