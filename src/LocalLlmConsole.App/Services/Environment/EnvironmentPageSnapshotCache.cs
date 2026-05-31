namespace LocalLlmConsole.Services;

public sealed class EnvironmentPageSnapshotCache
{
    private bool _windowsAutoRefreshStarted;
    private bool _wslAutoRefreshStarted;
    private WindowsToolSnapshot? _windowsTools;
    private WslEnvironmentReport? _wslReport;
    private WslToolSnapshot? _wslTools;

    public bool TryGetWindowsTools(out WindowsToolSnapshot tools)
    {
        if (_windowsTools is not null)
        {
            tools = _windowsTools;
            return true;
        }

        tools = default!;
        return false;
    }

    public void StoreWindowsTools(WindowsToolSnapshot tools)
    {
        ArgumentNullException.ThrowIfNull(tools);
        _windowsTools = tools;
    }

    public bool TryStartWindowsAutoRefresh()
    {
        if (_windowsAutoRefreshStarted)
            return false;

        _windowsAutoRefreshStarted = true;
        return true;
    }

    public bool TryGetWslTools(out WslEnvironmentReport report, out WslToolSnapshot tools)
    {
        if (_wslReport is not null && _wslTools is not null)
        {
            report = _wslReport;
            tools = _wslTools;
            return true;
        }

        report = default!;
        tools = default!;
        return false;
    }

    public void StoreWslTools(WslEnvironmentReport report, WslToolSnapshot tools)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(tools);
        _wslReport = report;
        _wslTools = tools;
    }

    public bool TryStartWslAutoRefresh()
    {
        if (_wslAutoRefreshStarted)
            return false;

        _wslAutoRefreshStarted = true;
        return true;
    }
}
