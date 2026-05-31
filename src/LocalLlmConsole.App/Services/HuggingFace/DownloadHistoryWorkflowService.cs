namespace LocalLlmConsole.Services;

public sealed record DownloadHistoryDeletePlan(
    DownloadJobPayload? Payload,
    bool IsActive,
    string DisplayName,
    string ConfirmationMessage);

public sealed record DownloadHistoryDeleteResult(
    bool Deleted,
    bool StopStillInProgress,
    string StatusMessage);

public sealed record DownloadHistoryResumePlan(
    bool CanResume,
    string StatusMessage);

public sealed record DownloadHistoryCommandResult(
    bool Applied,
    bool StartMonitor,
    string StatusMessage);

public sealed class DownloadHistoryWorkflowService
{
    private readonly StateStore _stateStore;
    private readonly IHuggingFaceDownloadOperations _downloads;

    public DownloadHistoryWorkflowService(StateStore stateStore, IHuggingFaceDownloadOperations downloads)
    {
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _downloads = downloads ?? throw new ArgumentNullException(nameof(downloads));
    }

    public Task<IReadOnlyList<JobRecord>> ListJobsAsync()
        => _stateStore.ListJobsAsync();

    public DownloadHistoryResumePlan BuildResumePlan(JobRecord job)
    {
        ArgumentNullException.ThrowIfNull(job);

        return job.Status switch
        {
            JobStatus.Running or JobStatus.Queued => new DownloadHistoryResumePlan(false, "That download is already active."),
            JobStatus.Completed => new DownloadHistoryResumePlan(false, "That download already completed."),
            _ => new DownloadHistoryResumePlan(true, "")
        };
    }

    public async Task<DownloadHistoryCommandResult> ResumeAsync(JobRecord job, AppSettings settings)
    {
        var plan = BuildResumePlan(job);
        if (!plan.CanResume)
            return new DownloadHistoryCommandResult(false, StartMonitor: false, plan.StatusMessage);

        await _downloads.ResumeDownloadAsync(job, settings);
        return new DownloadHistoryCommandResult(true, StartMonitor: true, $"Download started: {job.Id}");
    }

    public async Task<DownloadHistoryCommandResult> PauseAsync(JobRecord job)
    {
        ArgumentNullException.ThrowIfNull(job);

        await _downloads.PauseDownloadAsync(job);
        return new DownloadHistoryCommandResult(true, StartMonitor: false, $"Pause requested: {job.Id}");
    }

    public async Task<DownloadHistoryCommandResult> StopAsync(JobRecord job)
    {
        ArgumentNullException.ThrowIfNull(job);

        await _downloads.StopDownloadAsync(job);
        return new DownloadHistoryCommandResult(true, StartMonitor: false, $"Stop requested: {job.Id}");
    }

    public DownloadHistoryDeletePlan BuildDeletePlan(JobRecord job)
    {
        var payload = HuggingFaceService.ParseDownloadPayload(job.PayloadJson);
        var isActive = _downloads.IsDownloadActive(job.Id);
        var activeText = isActive
            ? "\n\nThis download is active. It will be stopped before the history entry is deleted."
            : "";
        var displayName = payload is null ? job.Id : $"{payload.File.Name}\n{payload.File.Repo}";
        return new DownloadHistoryDeletePlan(
            payload,
            isActive,
            displayName,
            $"Delete this model download history entry?\n\n{displayName}{activeText}\n\nIncomplete partial files are deleted. Completed model files are kept.");
    }

    public async Task<DownloadHistoryDeleteResult> DeleteAsync(
        JobRecord job,
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        var payload = HuggingFaceService.ParseDownloadPayload(job.PayloadJson);
        if (_downloads.IsDownloadActive(job.Id))
        {
            await _downloads.StopDownloadAsync(job);
            await WaitForDownloadToStopAsync(job.Id, cancellationToken);
            if (_downloads.IsDownloadActive(job.Id))
            {
                return new DownloadHistoryDeleteResult(
                    Deleted: false,
                    StopStillInProgress: true,
                    "Stop is still in progress. Try Delete again after the download stops.");
            }
        }
        else if (job.Status is JobStatus.Queued or JobStatus.Running or JobStatus.Paused)
        {
            await _downloads.StopDownloadAsync(job);
        }

        DeletePartialFile(settings.ModelsRoot, payload);
        await _stateStore.DeleteJobAsync(job.Id);
        return new DownloadHistoryDeleteResult(
            Deleted: true,
            StopStillInProgress: false,
            $"Deleted download history entry {job.Id}.");
    }

    public async Task<bool> HasReachedTerminalStatusAsync(string jobId)
    {
        var job = (await _stateStore.ListJobsAsync())
            .FirstOrDefault(job => string.Equals(job.Id, jobId, StringComparison.OrdinalIgnoreCase));
        return job?.Status is JobStatus.Completed or JobStatus.Failed or JobStatus.Cancelled or JobStatus.Paused or JobStatus.Interrupted;
    }

    public async Task WaitUntilInactiveOrTerminalAsync(
        string jobId,
        TimeSpan pollInterval,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);
        if (pollInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(pollInterval), pollInterval, "Poll interval must be positive.");

        while (_downloads.IsDownloadActive(jobId)
            && !await HasReachedTerminalStatusAsync(jobId))
            await Task.Delay(pollInterval, cancellationToken);
    }

    private async Task WaitForDownloadToStopAsync(string jobId, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 50 && _downloads.IsDownloadActive(jobId); attempt++)
            await Task.Delay(100, cancellationToken);
    }

    private static void DeletePartialFile(string modelsRoot, DownloadJobPayload? payload)
    {
        if (payload is null || string.IsNullOrWhiteSpace(payload.Destination)) return;
        var destination = Path.GetFullPath(payload.Destination);
        var root = Path.GetFullPath(modelsRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var relative = Path.GetRelativePath(root, destination);
        if (Path.IsPathRooted(relative)
            || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal)
            || string.Equals(relative, "..", StringComparison.Ordinal))
            return;

        var parent = Path.GetDirectoryName(destination);
        if (string.IsNullOrWhiteSpace(parent) || !FileSystemSafetyService.IsSafeChildDirectory(root, parent))
            return;

        var partial = destination + ".partial";
        if (File.Exists(partial))
            File.Delete(partial);

        if (Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any())
            Directory.Delete(parent);
    }
}
