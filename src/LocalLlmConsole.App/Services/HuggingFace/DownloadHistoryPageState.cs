namespace LocalLlmConsole.Services;

public sealed class DownloadHistoryPageState
{
    private readonly RefreshGate _refreshGate = new();

    public bool IsShowingHistory { get; private set; }

    public void ShowHistory()
        => IsShowingHistory = true;

    public void ShowSearch()
    {
        IsShowingHistory = false;
        _refreshGate.Complete();
    }

    public bool TryBeginTimerRefresh()
    {
        if (!IsShowingHistory)
            return false;

        return _refreshGate.TryBegin();
    }

    public void CompleteTimerRefresh()
        => _refreshGate.Complete();
}
