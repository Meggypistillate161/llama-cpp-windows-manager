namespace LocalLlmConsole.Services;

public sealed class GpuSummaryCache
{
    private static readonly TimeSpan Freshness = TimeSpan.FromSeconds(10);

    private string _summary = "Unavailable";
    private DateTimeOffset _capturedAt = DateTimeOffset.MinValue;

    public bool TryGet(DateTimeOffset now, out string summary)
    {
        if (_capturedAt != DateTimeOffset.MinValue && now - _capturedAt < Freshness)
        {
            summary = _summary;
            return true;
        }

        summary = "Unavailable";
        return false;
    }

    public string Store(string summary, DateTimeOffset capturedAt)
    {
        _summary = string.IsNullOrWhiteSpace(summary) ? "Unavailable" : summary;
        _capturedAt = capturedAt;
        return _summary;
    }

    public void Clear()
    {
        _summary = "Unavailable";
        _capturedAt = DateTimeOffset.MinValue;
    }
}
