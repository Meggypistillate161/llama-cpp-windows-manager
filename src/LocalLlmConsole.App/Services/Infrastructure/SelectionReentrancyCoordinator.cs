namespace LocalLlmConsole.Services;

public sealed class SelectionReentrancyCoordinator
{
    private bool _modelGridSelectionChanging;
    private bool _loadedSessionSelectionChanging;

    public bool IsModelGridSelectionChanging => _modelGridSelectionChanging;

    public bool IsLoadedSessionSelectionChanging => _loadedSessionSelectionChanging;

    public IDisposable? TryBeginModelGridSelection()
    {
        if (_modelGridSelectionChanging)
            return null;

        _modelGridSelectionChanging = true;
        return new SelectionLease(() => _modelGridSelectionChanging = false);
    }

    public IDisposable? TryBeginLoadedSessionSelection()
    {
        if (_loadedSessionSelectionChanging)
            return null;

        _loadedSessionSelectionChanging = true;
        return new SelectionLease(() => _loadedSessionSelectionChanging = false);
    }

    public IDisposable SuppressLoadedSessionSelection()
    {
        var previous = _loadedSessionSelectionChanging;
        _loadedSessionSelectionChanging = true;
        return new SelectionLease(() => _loadedSessionSelectionChanging = previous);
    }

    private sealed class SelectionLease : IDisposable
    {
        private Action? _release;

        public SelectionLease(Action release)
        {
            _release = release ?? throw new ArgumentNullException(nameof(release));
        }

        public void Dispose()
        {
            var release = Interlocked.Exchange(ref _release, null);
            release?.Invoke();
        }
    }
}
