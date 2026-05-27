using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Forms = System.Windows.Forms;
using WpfApplication = System.Windows.Application;
using WpfBinding = System.Windows.Data.Binding;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfProgressBar = System.Windows.Controls.ProgressBar;
using WpfTextBox = System.Windows.Controls.TextBox;
namespace LocalLlmConsole;

public partial class MainWindow
{
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

    private async Task TrackLifetimeTokenDeltasAsync(IReadOnlyList<RuntimeMetricPollResult> pollResults)
    {
        if (_stateStore is null)
        {
            ResetLifetimeCounters();
            return;
        }

        _lifetimeTokenCounters.RetainRuntimeKeys(pollResults.Select(result => result.RuntimeKey));
        foreach (var result in pollResults)
        {
            var generatedCounter = RuntimeGeneratedTokenCounter(result.Samples);
            var promptCounter = RuntimePromptTokenCounter(result.Samples);
            var delta = _lifetimeTokenCounters.Observe(
                result.RuntimeKey,
                result.Session.ModelId,
                result.Session.ModelName,
                generatedCounter,
                promptCounter,
                result.SlotSnapshot);

            if (!delta.HasTokens) continue;

            await _stateStore.AddTokenUsageAsync(delta.ModelId, delta.ModelName, delta.PromptTokens, delta.GeneratedTokens);
        }

        if (_viewModel.CurrentPage == "Lifetime") await RefreshLifetimeMetricsAsync();
    }

    private void ResetLifetimeCounters()
    {
        _lifetimeTokenCounters.Reset();
    }

    private void ResetLifetimeCounters(LoadedModelSessionSnapshot? session)
    {
        if (session is null)
            return;

        _lifetimeTokenCounters.Reset(RuntimeMetricKey(session));
    }

    private async Task ApplyIdleUnloadPoliciesAsync(IReadOnlyList<RuntimeMetricPollResult> pollResults)
    {
        if (_autoUnloadInProgress)
            return;

        var idleMinutes = _settings.AutoUnloadIdleMinutes;
        if (idleMinutes <= 0 || pollResults.Count == 0)
        {
            ResetIdleCounters();
            return;
        }

        _idleUnloadTracker.RetainRuntimeKeys(pollResults.Select(result => result.RuntimeKey));
        var now = DateTimeOffset.UtcNow;
        var idleSessions = new List<RuntimeMetricPollResult>();
        foreach (var result in pollResults)
        {
            var generatedCounter = RuntimeGeneratedTokenCounter(result.Samples);
            var promptCounter = RuntimePromptTokenCounter(result.Samples);
            if (_idleUnloadTracker.Observe(result.RuntimeKey, result.SlotSnapshot, generatedCounter, promptCounter, idleMinutes, now))
                idleSessions.Add(result);
        }

        if (idleSessions.Count == 0) return;

        _autoUnloadInProgress = true;
        try
        {
            foreach (var idle in idleSessions)
            {
                var model = await FindModelByIdAsync(idle.Session.ModelId);
                if (model is null) continue;

                SetStatus($"Auto-unloading {model.Name} after {idleMinutes} idle minute{(idleMinutes == 1 ? "" : "s")}.");
                await StopModelRuntimeAsync(model);
            }
        }
        finally
        {
            _autoUnloadInProgress = false;
        }
    }

    private void ResetIdleCounters()
    {
        _idleUnloadTracker.Reset();
    }

    private void ResetIdleCounters(LoadedModelSessionSnapshot? session)
    {
        if (session is null)
            return;

        _idleUnloadTracker.Reset(RuntimeMetricKey(session));
    }
}
