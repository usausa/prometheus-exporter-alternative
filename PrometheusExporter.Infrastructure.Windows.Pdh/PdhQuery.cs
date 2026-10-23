namespace PrometheusExporter.Infrastructure.Windows.Pdh;

public sealed class PdhQuery : IDisposable
{
    private readonly List<PdhCounterArray> counters = [];

    private nint query;

    private int collectCount;

    private bool disposed;

    private PdhQuery(nint query)
    {
        this.query = query;
    }

    public bool IsPrimed => collectCount >= 2;

    public static PdhQuery? Create() =>
        NativeMethods.PdhOpenQueryW(nint.Zero, nint.Zero, out var handle) == 0 ? new PdhQuery(handle) : null;

    public PdhCounterArray? AddCounterArray(string path)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (NativeMethods.PdhAddCounterW(query, path, nint.Zero, out var handle) != 0)
        {
            return null;
        }

        var counter = new PdhCounterArray(path, handle);
        counters.Add(counter);
        return counter;
    }

    public bool Collect()
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (NativeMethods.PdhCollectQueryData(query) != 0)
        {
            return false;
        }

        if (collectCount < 2)
        {
            collectCount++;
        }

        foreach (var counter in counters)
        {
            _ = counter.Read();
        }

        return true;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        if (query != nint.Zero)
        {
            _ = NativeMethods.PdhCloseQuery(query);
            query = nint.Zero;
        }

        counters.Clear();
    }
}
