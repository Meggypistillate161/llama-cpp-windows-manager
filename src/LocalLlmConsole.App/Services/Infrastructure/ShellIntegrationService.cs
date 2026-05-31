using System.Diagnostics;

namespace LocalLlmConsole.Services;

public sealed class ShellIntegrationService
{
    private readonly Action<ProcessStartInfo> _startProcess;

    public ShellIntegrationService(Action<ProcessStartInfo> startProcess)
    {
        _startProcess = startProcess ?? throw new ArgumentNullException(nameof(startProcess));
    }

    public void OpenFolder(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        Directory.CreateDirectory(path);
        OpenPath(path);
    }

    public void OpenPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        _startProcess(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    public void OpenUrl(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
            throw new ArgumentException("URL must be absolute.", nameof(url));

        _startProcess(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
