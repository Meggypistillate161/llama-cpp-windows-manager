namespace LocalLlmConsole.Services;

public sealed class RuntimeIdleUnloadPolicyService
{
    private readonly RuntimeIdleUnloadTracker _tracker;
    private bool _isApplying;

    public RuntimeIdleUnloadPolicyService()
        : this(new RuntimeIdleUnloadTracker())
    {
    }

    public RuntimeIdleUnloadPolicyService(RuntimeIdleUnloadTracker tracker)
    {
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
    }

    public bool IsApplying => _isApplying;

    public int TrackedRuntimeCount => _tracker.Count;

    public async Task<int> ApplyAsync(
        IReadOnlyList<RuntimeMetricPollResult> pollResults,
        int idleMinutes,
        DateTimeOffset now,
        Func<RuntimeMetricPollResult, CancellationToken, Task> unloadAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pollResults);
        ArgumentNullException.ThrowIfNull(unloadAsync);

        if (_isApplying)
            return 0;

        if (idleMinutes <= 0 || pollResults.Count == 0)
        {
            Reset();
            return 0;
        }

        _tracker.RetainRuntimeKeys(pollResults.Select(result => result.RuntimeKey));
        var idleSessions = IdleSessions(pollResults, idleMinutes, now);
        if (idleSessions.Count == 0)
            return 0;

        _isApplying = true;
        try
        {
            var unloaded = 0;
            foreach (var idle in idleSessions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await unloadAsync(idle, cancellationToken);
                unloaded++;
            }

            return unloaded;
        }
        finally
        {
            _isApplying = false;
        }
    }

    public void Reset()
        => _tracker.Reset();

    public void Reset(string runtimeKey)
        => _tracker.Reset(runtimeKey);

    private List<RuntimeMetricPollResult> IdleSessions(
        IReadOnlyList<RuntimeMetricPollResult> pollResults,
        int idleMinutes,
        DateTimeOffset now)
    {
        var idleSessions = new List<RuntimeMetricPollResult>();
        foreach (var result in pollResults)
        {
            var generatedCounter = RuntimeDashboardService.GeneratedTokenCounter(result.Samples);
            var promptCounter = RuntimeDashboardService.PromptTokenCounter(result.Samples);
            if (_tracker.Observe(result.RuntimeKey, result.SlotSnapshot, generatedCounter, promptCounter, idleMinutes, now))
                idleSessions.Add(result);
        }

        return idleSessions;
    }
}
