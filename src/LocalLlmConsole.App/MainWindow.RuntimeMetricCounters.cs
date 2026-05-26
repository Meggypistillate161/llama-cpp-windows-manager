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

    private async Task TrackLifetimeTokenDeltasAsync(string runtimeKey, double? generatedCounter, double? promptCounter)
    {
        if (_stateStore is null || !_llama.IsRunning || string.IsNullOrWhiteSpace(_llama.ActiveModelId))
        {
            ResetLifetimeCounters();
            return;
        }

        if (!string.Equals(runtimeKey, _lastLifetimeRuntimeKey, StringComparison.Ordinal))
        {
            _lastLifetimeRuntimeKey = runtimeKey;
            _lastLifetimeGeneratedCounter = generatedCounter;
            _lastLifetimePromptCounter = promptCounter;
            return;
        }

        var generatedDelta = RuntimeDashboardService.WholePositiveDelta(generatedCounter, _lastLifetimeGeneratedCounter);
        var promptDelta = RuntimeDashboardService.WholePositiveDelta(promptCounter, _lastLifetimePromptCounter);
        _lastLifetimeGeneratedCounter = generatedCounter;
        _lastLifetimePromptCounter = promptCounter;

        if (generatedDelta <= 0 && promptDelta <= 0) return;

        var modelName = await ActiveModelDisplayNameAsync(_llama.ActiveModelId);
        await _stateStore.AddTokenUsageAsync(_llama.ActiveModelId, modelName, promptDelta, generatedDelta);
        if (_viewModel.CurrentPage == "Lifetime") await RefreshLifetimeMetricsAsync();
    }

    private void ResetLifetimeCounters()
    {
        _lastLifetimeRuntimeKey = "";
        _lastLifetimePromptCounter = null;
        _lastLifetimeGeneratedCounter = null;
    }

    private async Task ApplyIdleUnloadPolicyAsync(string runtimeKey, RuntimeSlotSnapshot? slotSnapshot, double? generatedCounter, double? promptCounter)
    {
        if (_autoUnloadInProgress || !_llama.IsRunning || _llama.State != LlamaRuntimeState.Loaded)
        {
            if (!_llama.IsRunning) ResetIdleCounters();
            return;
        }

        var idleMinutes = _settings.AutoUnloadIdleMinutes;
        var observedGenerated = RuntimeDashboardService.MaxNullable(generatedCounter, slotSnapshot?.GeneratedTokens);
        var observedPrompt = RuntimeDashboardService.MaxNullable(promptCounter, slotSnapshot?.PromptTokensProcessed);
        var now = DateTimeOffset.UtcNow;

        if (!string.Equals(runtimeKey, _lastIdleRuntimeKey, StringComparison.Ordinal))
        {
            _lastIdleRuntimeKey = runtimeKey;
            _lastIdleGeneratedCounter = observedGenerated;
            _lastIdlePromptCounter = observedPrompt;
            _lastRuntimeActivityAt = now;
            return;
        }

        var hasTokenDelta = RuntimeDashboardService.PositiveDelta(observedGenerated, _lastIdleGeneratedCounter)
            || RuntimeDashboardService.PositiveDelta(observedPrompt, _lastIdlePromptCounter);
        var active = slotSnapshot?.IsProcessing == true || hasTokenDelta;
        _lastIdleGeneratedCounter = observedGenerated;
        _lastIdlePromptCounter = observedPrompt;

        if (active || _lastRuntimeActivityAt is null)
        {
            _lastRuntimeActivityAt = now;
            return;
        }

        if (idleMinutes <= 0) return;
        if ((now - _lastRuntimeActivityAt.Value).TotalMinutes < idleMinutes) return;

        _autoUnloadInProgress = true;
        try
        {
            var modelName = await ActiveModelDisplayNameAsync(_llama.ActiveModelId);
            SetStatus($"Auto-unloading {modelName} after {idleMinutes} idle minute{(idleMinutes == 1 ? "" : "s")}.");
            await StopLoadedRuntimeAsync();
        }
        finally
        {
            _autoUnloadInProgress = false;
        }
    }

    private void ResetIdleCounters()
    {
        _lastIdleRuntimeKey = "";
        _lastIdlePromptCounter = null;
        _lastIdleGeneratedCounter = null;
        _lastRuntimeActivityAt = null;
    }
}
