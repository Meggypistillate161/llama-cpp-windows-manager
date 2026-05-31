namespace LocalLlmConsole.Services;

public sealed class DebouncedAsyncAction : IDisposable
{
    private readonly TimeSpan _delay;
    private readonly object _gate = new();
    private CancellationTokenSource? _pending;

    public DebouncedAsyncAction(TimeSpan delay)
    {
        if (delay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(delay), "Delay cannot be negative.");
        _delay = delay;
    }

    public void Schedule(
        Func<CancellationToken, Task> action,
        Action<Func<Task>> runBackground)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(runBackground);

        var current = new CancellationTokenSource();
        CancellationTokenSource? previous;
        lock (_gate)
        {
            previous = _pending;
            _pending = current;
        }

        previous?.Cancel();
        try
        {
            runBackground(() => RunAsync(current, action));
        }
        catch
        {
            ClearIfCurrent(current);
            current.Dispose();
            throw;
        }
    }

    public void Cancel()
    {
        CancellationTokenSource? pending;
        lock (_gate)
        {
            pending = _pending;
            _pending = null;
        }

        pending?.Cancel();
    }

    public void Dispose()
        => Cancel();

    private async Task RunAsync(
        CancellationTokenSource cancellation,
        Func<CancellationToken, Task> action)
    {
        try
        {
            await Task.Delay(_delay, cancellation.Token);
            await action(cancellation.Token);
        }
        finally
        {
            ClearIfCurrent(cancellation);
            cancellation.Dispose();
        }
    }

    private void ClearIfCurrent(CancellationTokenSource cancellation)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_pending, cancellation))
                _pending = null;
        }
    }
}
