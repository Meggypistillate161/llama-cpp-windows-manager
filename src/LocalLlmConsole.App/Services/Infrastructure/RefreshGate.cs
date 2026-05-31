namespace LocalLlmConsole.Services;

public sealed class RefreshGate
{
    private bool _inFlight;

    public bool TryBegin()
    {
        if (_inFlight)
            return false;

        _inFlight = true;
        return true;
    }

    public void Complete()
        => _inFlight = false;
}
