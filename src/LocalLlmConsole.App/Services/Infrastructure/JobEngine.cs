
namespace LocalLlmConsole.Services;

public sealed class JobEngine
{
    private readonly StateStore _store;
    private readonly string _logRoot;

    public JobEngine(StateStore store, string logRoot)
    {
        _store = store;
        _logRoot = logRoot;
        Directory.CreateDirectory(_logRoot);
    }

    public async Task<JobRecord> CreateAsync(string kind, string payloadJson, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = DateTimeOffset.UtcNow;
        var id = $"{kind}-{Guid.NewGuid():N}";
        var job = new JobRecord(id, kind, JobStatus.Queued, payloadJson, Path.Combine(_logRoot, $"{id}.log"), now, now);
        await _store.UpsertJobAsync(job);
        return job;
    }

    public async Task UpdateStatusAsync(JobRecord job, JobStatus status, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await UpsertValidatedAsync(job, status, job.PayloadJson);
    }

    public async Task UpdateAsync(JobRecord job, JobStatus status, string payloadJson, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await UpsertValidatedAsync(job, status, payloadJson);
    }

    public Task RecoverAfterRestartAsync() => _store.MarkInterruptedJobsAsync();

    public static bool IsValidStatusTransition(JobStatus from, JobStatus to)
    {
        if (from == to)
            return true;

        return from switch
        {
            JobStatus.Queued => to is JobStatus.Running or JobStatus.Paused or JobStatus.Cancelled or JobStatus.Failed or JobStatus.Completed or JobStatus.Interrupted,
            JobStatus.Running => to is JobStatus.Paused or JobStatus.Cancelled or JobStatus.Failed or JobStatus.Completed or JobStatus.Interrupted,
            JobStatus.Paused => to is JobStatus.Queued or JobStatus.Cancelled or JobStatus.Failed or JobStatus.Completed,
            JobStatus.Cancelled or JobStatus.Failed or JobStatus.Interrupted => to is JobStatus.Queued or JobStatus.Cancelled or JobStatus.Failed or JobStatus.Completed,
            JobStatus.Completed => false,
            _ => false
        };
    }

    private async Task UpsertValidatedAsync(JobRecord job, JobStatus status, string payloadJson)
    {
        var current = await _store.GetJobAsync(job.Id) ?? job;
        if (!IsValidStatusTransition(current.Status, status))
            throw new InvalidOperationException($"Invalid job status transition for {job.Id}: {current.Status} -> {status}.");

        await _store.UpsertJobAsync(current with
        {
            Status = status,
            PayloadJson = payloadJson,
            UpdatedAt = DateTimeOffset.UtcNow
        });
    }
}
