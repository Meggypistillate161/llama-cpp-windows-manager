namespace LocalLlmConsole.Services;

public sealed partial class LlamaProcessSupervisor
{
    public void Stop()
    {
        if (_lastRuntimeMode == RuntimeMode.Native)
            _nativeRuntimeStop.Stop(_process);
        else
            StopHostProcess();

        if (_lastSettings is not null && _lastRuntimeMode == RuntimeMode.Wsl)
        {
            _wslRuntimeStop.Stop(new WslRuntimeStopRequest(
                _lastSettings,
                _lastRuntimeExecutablePath,
                _lastWslProcessMarker,
                LogPath,
                BoundedLogFile.MegabytesToBytes(_lastSettings.MaxLogFileSizeMb)));
        }

        try { _process?.Dispose(); } catch { }
        try { _log?.Dispose(); } catch { }
        _process = null;
        _log = null;
        ActiveModelId = "";
        ActiveRuntimeId = "";
        State = LlamaRuntimeState.Stopped;
        LastExitCode = null;
        _lastSettings = null;
        _lastRuntimeExecutablePath = "";
        _lastWslProcessMarker = "";
        _lastApiKey = "";
        _attached = false;
        _recovered = false;
    }

    private void StopHostProcess()
    {
        try
        {
            if (_process is { HasExited: false })
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(3000);
            }
        }
        catch { }
    }

    public void Dispose() => Stop();
}
