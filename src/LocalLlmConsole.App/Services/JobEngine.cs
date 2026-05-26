
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
        await _store.UpsertJobAsync(job with { Status = status, UpdatedAt = DateTimeOffset.UtcNow });
    }

    public async Task UpdateAsync(JobRecord job, JobStatus status, string payloadJson, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _store.UpsertJobAsync(job with { Status = status, PayloadJson = payloadJson, UpdatedAt = DateTimeOffset.UtcNow });
    }

    public Task RecoverAfterRestartAsync() => _store.MarkInterruptedJobsAsync();
}
