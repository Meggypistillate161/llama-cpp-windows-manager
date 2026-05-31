namespace LocalLlmConsole.Services;

public sealed record RuntimeMetricDisplaySnapshot(
    string RuntimeKey,
    IReadOnlyList<PrometheusSample> Samples,
    string GenerationRate,
    string TotalTokens,
    string Settings,
    DateTimeOffset CapturedAt);

public sealed record RuntimeMetricSummaryResult(
    string GenerationRate,
    string TotalTokens,
    string Settings,
    bool UsedLastKnown,
    DateTimeOffset? LastKnownCapturedAt);

public sealed class RuntimeMetricSummaryTracker
{
    private string _lastMetricRuntimeKey = "";
    private double? _lastPredictedTokenCounter;
    private double? _lastPromptTokenCounter;
    private DateTimeOffset? _lastMetricPollAt;
    private string _lastSlotRuntimeKey = "";
    private double? _lastSlotPromptProcessedCounter;
    private double? _lastSlotGeneratedCounter;
    private DateTimeOffset? _lastSlotPollAt;
    private RuntimeMetricDisplaySnapshot? _lastDisplay;

    public RuntimeMetricSummaryResult Apply(
        string runtimeKey,
        IReadOnlyList<PrometheusSample> samples,
        AppSettings metricsSettings,
        RuntimeSlotSnapshot? slotSnapshot,
        DateTimeOffset? capturedAt = null)
    {
        if (!string.Equals(runtimeKey, _lastMetricRuntimeKey, StringComparison.Ordinal))
        {
            ResetCounters();
            _lastMetricRuntimeKey = runtimeKey;
        }

        if (samples.Count == 0
            && slotSnapshot is null
            && _lastDisplay is { } snapshot
            && string.Equals(snapshot.RuntimeKey, runtimeKey, StringComparison.Ordinal))
        {
            return new RuntimeMetricSummaryResult(
                snapshot.GenerationRate,
                snapshot.TotalTokens,
                snapshot.Settings,
                UsedLastKnown: true,
                snapshot.CapturedAt);
        }

        var now = capturedAt ?? DateTimeOffset.UtcNow;
        var predictedTokens = RuntimeDashboardService.GeneratedTokenCounter(samples);
        var predictedSeconds = RuntimeMetrics.Sum(samples, ["tokens", "predicted", "seconds", "total"], [])
            ?? RuntimeMetrics.Sum(samples, ["tokens", "generated", "seconds", "total"], [])
            ?? RuntimeMetrics.Sum(samples, ["eval", "time"], ["prompt"]);
        var promptTokens = RuntimeDashboardService.PromptTokenCounter(samples);
        var promptSeconds = RuntimeMetrics.Sum(samples, ["prompt", "seconds", "total"], [])
            ?? RuntimeMetrics.Sum(samples, ["prompt", "time"], []);

        var liveGenerationRate = RuntimeDashboardService.CounterRate(predictedTokens, _lastPredictedTokenCounter, now, _lastMetricPollAt, 0.5);
        var livePromptRate = RuntimeDashboardService.CounterRate(promptTokens, _lastPromptTokenCounter, now, _lastMetricPollAt, 0.5);
        _lastPredictedTokenCounter = predictedTokens;
        _lastPromptTokenCounter = promptTokens;
        _lastMetricPollAt = now;

        var (slotPromptRate, slotGenerationRate) = SlotLiveRates(slotSnapshot, now, runtimeKey);
        liveGenerationRate = slotGenerationRate ?? liveGenerationRate;
        livePromptRate = slotPromptRate ?? livePromptRate;

        var averageGenerationRate = RuntimeMetrics.First(samples, ["predicted", "tokens", "seconds"], ["total"])
            ?? RuntimeMetrics.First(samples, ["generation", "tokens", "seconds"], ["total"])
            ?? RuntimeDashboardService.Rate(predictedTokens, predictedSeconds);
        var averagePromptRate = RuntimeMetrics.First(samples, ["prompt", "tokens", "seconds"], ["total"])
            ?? RuntimeDashboardService.Rate(promptTokens, promptSeconds);
        var kvUsage = RuntimeMetrics.First(samples, ["kv", "cache", "usage"], []);
        var kvTokens = RuntimeMetrics.Sum(samples, ["kv", "cache", "tokens"], [])
            ?? RuntimeMetrics.Sum(samples, ["kv", "tokens"], []);
        var contextSize = RuntimeMetrics.First(samples, ["context", "size"], [])
            ?? RuntimeMetrics.First(samples, ["ctx", "size"], [])
            ?? slotSnapshot?.ContextSize
            ?? (metricsSettings.ContextSize > 0 ? (double?)metricsSettings.ContextSize : null);
        kvTokens ??= slotSnapshot?.ContextTokens;

        var displayGeneratedTokens = RuntimeDashboardService.MaxNullable(predictedTokens, slotSnapshot?.GeneratedTokens);
        var displayPromptTokens = RuntimeDashboardService.MaxNullable(promptTokens, slotSnapshot?.PromptTokensProcessed);

        var generationRateText = $"Gen {RuntimeDashboardService.RateLabel(liveGenerationRate, averageGenerationRate)}\nPrompt {RuntimeDashboardService.RateLabel(livePromptRate, averagePromptRate)}";
        var totalTokensText = RuntimeDashboardService.TokenSummaryLabel(displayGeneratedTokens, displayPromptTokens);
        var settingsText = RuntimeDashboardService.RuntimeSettingsLabel(kvUsage, kvTokens, contextSize, metricsSettings.ContextSize);

        Remember(runtimeKey, samples, generationRateText, totalTokensText, settingsText, displayGeneratedTokens, displayPromptTokens, now);
        return new RuntimeMetricSummaryResult(
            generationRateText,
            totalTokensText,
            settingsText,
            UsedLastKnown: false,
            LastKnownCapturedAt: null);
    }

    public IReadOnlyList<PrometheusSample> LastKnownSamples(string runtimeKey)
        => _lastDisplay is { Samples.Count: > 0 } snapshot
           && string.Equals(snapshot.RuntimeKey, runtimeKey, StringComparison.Ordinal)
            ? snapshot.Samples
            : [];

    public void Reset()
    {
        ResetCounters();
        _lastMetricRuntimeKey = "";
        _lastDisplay = null;
    }

    private (double? PromptRate, double? GenerationRate) SlotLiveRates(RuntimeSlotSnapshot? snapshot, DateTimeOffset now, string runtimeKey)
    {
        if (snapshot is null)
        {
            _lastSlotRuntimeKey = runtimeKey;
            _lastSlotPromptProcessedCounter = null;
            _lastSlotGeneratedCounter = null;
            _lastSlotPollAt = null;
            return (null, null);
        }

        if (!string.Equals(runtimeKey, _lastSlotRuntimeKey, StringComparison.Ordinal))
        {
            _lastSlotRuntimeKey = runtimeKey;
            _lastSlotPromptProcessedCounter = null;
            _lastSlotGeneratedCounter = null;
            _lastSlotPollAt = null;
        }

        double? promptRate = null;
        double? generationRate = null;
        if (_lastSlotPollAt is not null)
        {
            var elapsed = (now - _lastSlotPollAt.Value).TotalSeconds;
            if (elapsed >= 0.25)
            {
                promptRate = RuntimeDashboardService.DeltaRate(snapshot.PromptTokensProcessed, _lastSlotPromptProcessedCounter, elapsed, snapshot.IsProcessing);
                generationRate = RuntimeDashboardService.DeltaRate(snapshot.GeneratedTokens, _lastSlotGeneratedCounter, elapsed, snapshot.IsProcessing);
            }
        }

        _lastSlotPromptProcessedCounter = snapshot.PromptTokensProcessed;
        _lastSlotGeneratedCounter = snapshot.GeneratedTokens;
        _lastSlotPollAt = now;
        return (promptRate, generationRate);
    }

    private void Remember(
        string runtimeKey,
        IReadOnlyList<PrometheusSample> samples,
        string generationRateText,
        string totalTokensText,
        string settingsText,
        double? displayGeneratedTokens,
        double? displayPromptTokens,
        DateTimeOffset capturedAt)
    {
        if (displayGeneratedTokens is null && displayPromptTokens is null && samples.Count == 0)
            return;

        var cachedSamples = samples.Count > 0
            ? samples.ToArray()
            : _lastDisplay is { } previous && string.Equals(previous.RuntimeKey, runtimeKey, StringComparison.Ordinal)
                ? previous.Samples
                : [];

        _lastDisplay = new RuntimeMetricDisplaySnapshot(
            runtimeKey,
            cachedSamples,
            generationRateText,
            totalTokensText,
            settingsText,
            capturedAt);
    }

    private void ResetCounters()
    {
        _lastPredictedTokenCounter = null;
        _lastPromptTokenCounter = null;
        _lastMetricPollAt = null;
        _lastSlotRuntimeKey = "";
        _lastSlotPromptProcessedCounter = null;
        _lastSlotGeneratedCounter = null;
        _lastSlotPollAt = null;
    }
}
