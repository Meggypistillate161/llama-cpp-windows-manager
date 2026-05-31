namespace LocalLlmConsole.Services;

public enum CacheClearPlanStatus
{
    Ready,
    UnsafeRoot,
    Busy,
    Empty
}

public sealed record CacheClearPlan(
    CacheClearPlanStatus Status,
    long SizeBytes,
    string DisplaySize,
    string Message);

public sealed class CacheClearWorkflowService
{
    private readonly string _workspaceRoot;
    private readonly StateStore _stateStore;

    public CacheClearWorkflowService(string workspaceRoot, StateStore stateStore)
    {
        _workspaceRoot = string.IsNullOrWhiteSpace(workspaceRoot)
            ? throw new ArgumentException("Workspace root is required.", nameof(workspaceRoot))
            : workspaceRoot;
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
    }

    public async Task<CacheClearPlan> PlanAsync(
        AppSettings settings,
        bool hasActiveDownloads,
        CancellationToken cancellationToken = default)
    {
        if (!CacheMaintenanceService.IsSafeCacheRoot(_workspaceRoot, settings.CacheRoot))
        {
            return new CacheClearPlan(
                CacheClearPlanStatus.UnsafeRoot,
                0,
                "",
                "The cache folder is outside the app workspace or contains a junction/symlink, so it was not cleared.");
        }

        if (hasActiveDownloads || await HasRunningCacheJobAsync())
        {
            return new CacheClearPlan(
                CacheClearPlanStatus.Busy,
                0,
                "",
                "Downloads or runtime builds are still using the cache. Stop or finish them before clearing it.");
        }

        var size = await Task.Run(() => CacheMaintenanceService.Size(settings.CacheRoot), cancellationToken);
        var displaySize = DisplayFormatService.BytesOrZero(size);
        if (size <= 0)
        {
            return new CacheClearPlan(
                CacheClearPlanStatus.Empty,
                size,
                displaySize,
                "No cache files are currently stored.");
        }

        return new CacheClearPlan(
            CacheClearPlanStatus.Ready,
            size,
            displaySize,
            $"Clear {displaySize} from the app cache?\n\nRuntime sources, temporary build files, partial update downloads, and other disposable cache data will be removed.");
    }

    public Task ClearAsync(AppSettings settings, CancellationToken cancellationToken = default)
        => Task.Run(() => CacheMaintenanceService.ClearSafeCacheRoot(_workspaceRoot, settings.CacheRoot), cancellationToken);

    private async Task<bool> HasRunningCacheJobAsync()
    {
        var jobs = await _stateStore.ListJobsAsync();
        return jobs.Any(job =>
            job.Status is JobStatus.Queued or JobStatus.Running or JobStatus.Paused
            && (job.Kind.Contains("runtime", StringComparison.OrdinalIgnoreCase)
                || job.Kind.Contains("download", StringComparison.OrdinalIgnoreCase)));
    }
}
