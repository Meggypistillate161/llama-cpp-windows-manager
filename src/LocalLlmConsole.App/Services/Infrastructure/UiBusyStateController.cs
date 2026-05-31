namespace LocalLlmConsole.Services;

public sealed class UiBusyStateController
{
    private bool _pageWasEnabled = true;
    private bool _hasActiveBusyState;

    public bool HasActiveBusyState => _hasActiveBusyState;

    public void Begin(
        bool pageIsEnabled,
        Action<bool> setPageEnabled,
        Action<bool> setWaitCursor)
    {
        ArgumentNullException.ThrowIfNull(setPageEnabled);
        ArgumentNullException.ThrowIfNull(setWaitCursor);

        if (_hasActiveBusyState)
            return;

        _pageWasEnabled = pageIsEnabled;
        _hasActiveBusyState = true;
        setPageEnabled(false);
        setWaitCursor(true);
    }

    public bool End(
        Action<bool> setPageEnabled,
        Action<bool> setWaitCursor)
    {
        ArgumentNullException.ThrowIfNull(setPageEnabled);
        ArgumentNullException.ThrowIfNull(setWaitCursor);

        if (!_hasActiveBusyState)
            return false;

        setPageEnabled(_pageWasEnabled);
        setWaitCursor(false);
        _hasActiveBusyState = false;
        return true;
    }
}
