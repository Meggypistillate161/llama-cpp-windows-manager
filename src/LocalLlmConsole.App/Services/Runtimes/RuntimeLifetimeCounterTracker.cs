namespace LocalLlmConsole.Services;

public sealed record TokenUsageDelta(string ModelId, string ModelName, long PromptTokens, long GeneratedTokens)
{
    public static TokenUsageDelta Empty { get; } = new("", "", 0, 0);
    public bool HasTokens => PromptTokens > 0 || GeneratedTokens > 0;
}

public sealed class RuntimeLifetimeCounterTracker
{
    private sealed class CounterState
    {
        public double? PromptCounter;
        public double? GeneratedCounter;
    }

    private readonly Dictionary<string, CounterState> _states = new(StringComparer.Ordinal);

    public int Count => _states.Count;

    public TokenUsageDelta Observe(
        string runtimeKey,
        string modelId,
        string modelName,
        double? generatedCounter,
        double? promptCounter,
        RuntimeSlotSnapshot? slotSnapshot)
    {
        if (string.IsNullOrWhiteSpace(runtimeKey) || string.IsNullOrWhiteSpace(modelId))
            return TokenUsageDelta.Empty;

        var observedGenerated = RuntimeDashboardService.MaxNullable(generatedCounter, slotSnapshot?.GeneratedTokens);
        var observedPrompt = RuntimeDashboardService.MaxNullable(promptCounter, slotSnapshot?.PromptTokensProcessed);
        if (observedGenerated is null && observedPrompt is null)
            return TokenUsageDelta.Empty;

        if (!_states.TryGetValue(runtimeKey, out var state))
        {
            _states[runtimeKey] = new CounterState
            {
                GeneratedCounter = observedGenerated,
                PromptCounter = observedPrompt
            };
            return TokenUsageDelta.Empty;
        }

        var generatedDelta = RuntimeDashboardService.WholePositiveDeltaAndRemember(observedGenerated, ref state.GeneratedCounter);
        var promptDelta = RuntimeDashboardService.WholePositiveDeltaAndRemember(observedPrompt, ref state.PromptCounter);
        if (generatedDelta <= 0 && promptDelta <= 0)
            return TokenUsageDelta.Empty;

        return new TokenUsageDelta(modelId, modelName, promptDelta, generatedDelta);
    }

    public void Reset() => _states.Clear();

    public void Reset(string runtimeKey)
    {
        if (!string.IsNullOrWhiteSpace(runtimeKey))
            _states.Remove(runtimeKey);
    }

    public void RetainRuntimeKeys(IEnumerable<string> runtimeKeys)
    {
        var active = runtimeKeys.ToHashSet(StringComparer.Ordinal);
        foreach (var key in _states.Keys.Where(key => !active.Contains(key)).ToArray())
            _states.Remove(key);
    }
}
