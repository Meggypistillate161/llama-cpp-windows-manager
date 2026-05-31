namespace LocalLlmConsole.Services;

public sealed class AppLogApplicationService
{
    private readonly string _workspaceRoot;
    private readonly Func<DateTimeOffset> _now;

    public AppLogApplicationService(string workspaceRoot, Func<DateTimeOffset>? now = null)
    {
        _workspaceRoot = string.IsNullOrWhiteSpace(workspaceRoot)
            ? throw new ArgumentException("Workspace root is required.", nameof(workspaceRoot))
            : workspaceRoot;
        _now = now ?? (() => DateTimeOffset.Now);
    }

    public async Task WriteExceptionAsync(
        Exception exception,
        string apiKey,
        long maxLogBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(exception);

        try
        {
            var now = _now();
            var logRoot = Path.Combine(_workspaceRoot, "logs");
            Directory.CreateDirectory(logRoot);
            var path = Path.Combine(logRoot, $"app-{now:yyyyMMdd}.log");
            var text = $"[{now:O}] ERROR {exception.GetType().Name}: {exception.Message}{Environment.NewLine}{exception}{Environment.NewLine}";
            text = LogFileService.RedactSensitiveText(text, apiKey);
            cancellationToken.ThrowIfCancellationRequested();
            await BoundedLogFile.AppendAsync(path, text, maxLogBytes);
        }
        catch
        {
            // Logging must never create a second failure path.
        }
    }
}
