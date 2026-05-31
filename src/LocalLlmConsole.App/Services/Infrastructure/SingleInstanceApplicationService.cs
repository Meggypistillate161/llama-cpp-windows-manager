using System.Threading;

namespace LocalLlmConsole.Services;

public interface ISingleInstanceLease : IDisposable
{
    bool OwnsInstance { get; }

    void Release();
}

public sealed class SingleInstanceApplicationService : IDisposable
{
    private readonly Func<string, ISingleInstanceLease> _acquireLease;
    private ISingleInstanceLease? _lease;

    public SingleInstanceApplicationService(Func<string, ISingleInstanceLease> acquireLease)
    {
        _acquireLease = acquireLease ?? throw new ArgumentNullException(nameof(acquireLease));
    }

    public static ISingleInstanceLease AcquireMutexLease(string mutexName)
        => SingleInstanceMutexLease.Acquire(mutexName);

    public bool TryAcquire(string mutexName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mutexName);

        if (_lease is not null)
            return _lease.OwnsInstance;

        var lease = _acquireLease(mutexName);
        if (!lease.OwnsInstance)
        {
            lease.Dispose();
            return false;
        }

        _lease = lease;
        return true;
    }

    public void Release()
    {
        if (_lease is null)
            return;

        if (_lease.OwnsInstance)
            _lease.Release();

        _lease.Dispose();
        _lease = null;
    }

    public void Dispose()
        => Release();

    private sealed class SingleInstanceMutexLease : ISingleInstanceLease
    {
        private Mutex? _mutex;
        private bool _ownsInstance;

        private SingleInstanceMutexLease(Mutex mutex, bool ownsInstance)
        {
            _mutex = mutex;
            _ownsInstance = ownsInstance;
        }

        public bool OwnsInstance => _ownsInstance;

        public static ISingleInstanceLease Acquire(string mutexName)
        {
            var mutex = new Mutex(initiallyOwned: true, mutexName, out var ownsInstance);
            return new SingleInstanceMutexLease(mutex, ownsInstance);
        }

        public void Release()
        {
            if (!_ownsInstance || _mutex is null)
                return;

            _mutex.ReleaseMutex();
            _ownsInstance = false;
        }

        public void Dispose()
        {
            _mutex?.Dispose();
            _mutex = null;
        }
    }
}
