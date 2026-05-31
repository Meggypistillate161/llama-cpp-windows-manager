namespace LocalLlmConsole.Services;

public sealed class ClipboardService
{
    private readonly Action<string> _setText;

    public ClipboardService(Action<string> setText)
    {
        _setText = setText ?? throw new ArgumentNullException(nameof(setText));
    }

    public void SetText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _setText(text);
    }
}
