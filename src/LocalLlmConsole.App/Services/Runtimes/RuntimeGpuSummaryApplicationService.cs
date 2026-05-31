namespace LocalLlmConsole.Services;

public sealed class RuntimeGpuSummaryApplicationService
{
    private readonly GpuStatusProbeService _gpuStatus;
    private readonly GpuSummaryCache _cache;
    private readonly Func<string> _wslExe;

    public RuntimeGpuSummaryApplicationService(
        GpuStatusProbeService gpuStatus,
        GpuSummaryCache cache,
        Func<string> wslExe)
    {
        _gpuStatus = gpuStatus ?? throw new ArgumentNullException(nameof(gpuStatus));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _wslExe = wslExe ?? throw new ArgumentNullException(nameof(wslExe));
    }

    public async Task<string> SummaryAsync(
        LoadedModelSessionSnapshot? activeSession,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        if (_cache.TryGet(now, out var cachedSummary))
            return cachedSummary;

        var summary = activeSession?.Backend == RuntimeBackend.Sycl
            ? activeSession.Mode == RuntimeMode.Wsl
                ? await _gpuStatus.WslIntelArcSummaryAsync(_wslExe(), activeSession.LaunchSettings.WslDistro, cancellationToken)
                : await _gpuStatus.WindowsIntelArcSummaryAsync(cancellationToken)
            : await _gpuStatus.SummaryAsync(cancellationToken);

        return _cache.Store(summary, now);
    }
}
