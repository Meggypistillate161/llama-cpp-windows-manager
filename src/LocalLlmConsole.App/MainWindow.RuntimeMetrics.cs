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
    private void PopulateRuntimeMetricRows(IReadOnlyList<PrometheusSample> samples)
    {
        _viewModel.RuntimeMetrics.ReplaceSamples(samples);
        _runtimeMetricsGrid?.Items.Refresh();
    }

    private void PopulateRuntimeMetricRowsOrLastKnown(string error, string runtimeKey)
    {
        if (_lastRuntimeMetricDisplay is { Samples.Count: > 0 } snapshot
            && string.Equals(snapshot.RuntimeKey, runtimeKey, StringComparison.Ordinal))
        {
            _viewModel.RuntimeMetrics.ReplaceSamples(snapshot.Samples);
            _viewModel.RuntimeMetrics.Rows.Insert(0, new UiRow
            {
                C1 = "metrics_status",
                C2 = "",
                C3 = $"Last known values; refresh paused ({error})",
                C4 = "status",
                C5 = "The runtime did not return fresh metrics on the last poll."
            });
            _runtimeMetricsGrid?.Items.Refresh();
            return;
        }

        PopulateRuntimeMetricRows([new PrometheusSample("metrics_error", "", double.NaN, error, "error", "llama.cpp has not returned metrics yet.")]);
    }

    private Task ApplyRuntimeMetricSummaryAsync(IReadOnlyList<PrometheusSample> samples, AppSettings metricsSettings, RuntimeSlotSnapshot? slotSnapshot, string runtimeKey)
    {
        if (!string.Equals(runtimeKey, _lastMetricRuntimeKey, StringComparison.Ordinal))
        {
            ResetMetricCounters();
            _lastMetricRuntimeKey = runtimeKey;
        }

        if (samples.Count == 0 && slotSnapshot is null && ApplyLastKnownRuntimeMetricSummary(runtimeKey))
            return Task.CompletedTask;

        var predictedTokens = RuntimeGeneratedTokenCounter(samples);
        var predictedSeconds = RuntimeMetrics.Sum(samples, ["tokens", "predicted", "seconds", "total"], [])
            ?? RuntimeMetrics.Sum(samples, ["tokens", "generated", "seconds", "total"], [])
            ?? RuntimeMetrics.Sum(samples, ["eval", "time"], ["prompt"]);
        var promptTokens = RuntimePromptTokenCounter(samples);
        var promptSeconds = RuntimeMetrics.Sum(samples, ["prompt", "seconds", "total"], [])
            ?? RuntimeMetrics.Sum(samples, ["prompt", "time"], []);

        var now = DateTimeOffset.UtcNow;
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

        SetMetricText(_runtimeDashboardGenerationRate, generationRateText);
        SetMetricText(_runtimeDashboardTotalTokens, totalTokensText);
        SetMetricText(_runtimeDashboardRequests, settingsText);
        RememberRuntimeMetricDisplay(runtimeKey, samples, generationRateText, totalTokensText, settingsText, displayGeneratedTokens, displayPromptTokens);
        return Task.CompletedTask;
    }

    private static double? RuntimeGeneratedTokenCounter(IReadOnlyList<PrometheusSample> samples)
        => RuntimeMetrics.Sum(samples, ["tokens", "predicted", "total"], ["seconds", "duration"])
           ?? RuntimeMetrics.Sum(samples, ["tokens", "generated", "total"], ["seconds", "duration"])
           ?? RuntimeMetrics.Sum(samples, ["tokens", "eval", "total"], ["seconds", "duration"]);

    private static double? RuntimePromptTokenCounter(IReadOnlyList<PrometheusSample> samples)
        => RuntimeMetrics.Sum(samples, ["prompt", "tokens", "total"], ["seconds", "duration"]);

    private static string RuntimeMetricKey(LoadedModelSessionSnapshot session)
        => $"{session.ModelId}|{session.RuntimeId}|{session.LaunchSettings.Port}";

    private string CurrentRuntimeMetricKey(AppSettings metricsSettings)
        => _sessions.SelectedSnapshot() is { } selected
            ? RuntimeMetricKey(selected)
            : $"{_llama.ActiveModelId}|{_llama.ActiveRuntimeId}|{metricsSettings.Port}";

    private bool ApplyLastKnownRuntimeMetricSummary(string runtimeKey)
    {
        if (_lastRuntimeMetricDisplay is not { } snapshot
            || !string.Equals(snapshot.RuntimeKey, runtimeKey, StringComparison.Ordinal))
            return false;

        var now = DateTimeOffset.UtcNow;
        SetMetricText(_runtimeDashboardGenerationRate, RuntimeDashboardService.WithLastKnownLine(snapshot.GenerationRate, snapshot.CapturedAt, now));
        SetMetricText(_runtimeDashboardTotalTokens, RuntimeDashboardService.WithLastKnownLine(snapshot.TotalTokens, snapshot.CapturedAt, now));
        SetMetricText(_runtimeDashboardRequests, snapshot.Settings);
        return true;
    }

    private void RememberRuntimeMetricDisplay(
        string runtimeKey,
        IReadOnlyList<PrometheusSample> samples,
        string generationRateText,
        string totalTokensText,
        string settingsText,
        double? displayGeneratedTokens,
        double? displayPromptTokens)
    {
        if (displayGeneratedTokens is null && displayPromptTokens is null && samples.Count == 0)
            return;

        var cachedSamples = samples.Count > 0
            ? samples.ToArray()
            : _lastRuntimeMetricDisplay is { } previous && string.Equals(previous.RuntimeKey, runtimeKey, StringComparison.Ordinal)
                ? previous.Samples
                : [];

        _lastRuntimeMetricDisplay = new RuntimeMetricDisplaySnapshot(
            runtimeKey,
            cachedSamples,
            generationRateText,
            totalTokensText,
            settingsText,
            DateTimeOffset.UtcNow);
    }

    private async Task<RuntimeSlotSnapshot?> RuntimeSlotSnapshotAsync(AppSettings settings)
    {
        try
        {
            var raw = await RuntimeEndpointService.RuntimeGetStringAsync(_metricsClient, $"{RuntimeEndpointService.LocalServerBaseUrl(settings)}/slots", settings);
            return RuntimeDashboardService.ParseSlotSnapshot(raw);
        }
        catch
        {
            return null;
        }
    }

    private void UpdateRuntimeModelProgress()
        => SetRuntimeModelProgress(_llama.State);

    private void SetRuntimeModelProgress(LlamaRuntimeState state)
    {
        if (_runtimeDashboardModelProgress is null) return;

        switch (state)
        {
            case LlamaRuntimeState.Loading:
                _runtimeDashboardModelProgress.Visibility = Visibility.Visible;
                _runtimeDashboardModelProgress.IsIndeterminate = true;
                _runtimeDashboardModelProgress.Value = 0;
                break;
            case LlamaRuntimeState.Loaded:
                _runtimeDashboardModelProgress.Visibility = Visibility.Visible;
                _runtimeDashboardModelProgress.IsIndeterminate = false;
                _runtimeDashboardModelProgress.Value = 100;
                break;
            default:
                _runtimeDashboardModelProgress.Visibility = Visibility.Collapsed;
                _runtimeDashboardModelProgress.IsIndeterminate = false;
                _runtimeDashboardModelProgress.Value = 0;
                break;
        }
    }

    private void SetRuntimeMetricSummary(string generationRate, string totalTokens, string settings)
    {
        SetMetricText(_runtimeDashboardGenerationRate, generationRate);
        SetMetricText(_runtimeDashboardTotalTokens, totalTokens);
        SetMetricText(_runtimeDashboardRequests, settings);
    }

    private async Task<string> CachedGpuSummaryAsync()
    {
        var now = DateTimeOffset.UtcNow;
        if (now - _cachedGpuSummaryAt < TimeSpan.FromSeconds(10))
            return _cachedGpuSummary;

        var active = _sessions.SelectedSnapshot();
        _cachedGpuSummary = active?.Backend == RuntimeBackend.Sycl
            ? active.Mode == RuntimeMode.Wsl
                ? await GpuStatusService.WslIntelArcSummaryAsync(HostExecutableResolver.WslExe(), active.LaunchSettings.WslDistro)
                : await GpuStatusService.WindowsIntelArcSummaryAsync()
            : await GpuStatusService.SummaryAsync();
        _cachedGpuSummaryAt = DateTimeOffset.UtcNow;
        return _cachedGpuSummary;
    }

    private void RefreshRuntimeLogTail(RuntimeSlotSnapshot? slotSnapshot = null)
    {
        if (_overviewRuntimeLogBox is null) return;
        if (string.IsNullOrWhiteSpace(_llama.LogPath) || !File.Exists(_llama.LogPath))
        {
            _overviewRuntimeLogBox.Text = _llama.IsRunning ? "Runtime log file has not been created yet." : "No runtime log is active.";
            return;
        }

        try
        {
            var heading = _llama.IsRunning ? $"Live log: {_llama.LogPath}" : $"Last runtime log: {_llama.LogPath}";
            var slotStatus = slotSnapshot is null
                ? ""
                : slotSnapshot.IsProcessing
                    ? $"Slot status: processing | Prompt {slotSnapshot.PromptTokensProcessed:N0}/{(slotSnapshot.PromptTokens?.ToString("N0") ?? "?")} | Gen {slotSnapshot.GeneratedTokens:N0}"
                    : "Slot status: idle";
            var rawTail = LogFileService.Tail(_llama.LogPath, 16000);
            var logTail = LogFileService.CollapseIdleSlotNoise(rawTail);
            _overviewRuntimeLogBox.Text = string.IsNullOrWhiteSpace(slotStatus)
                ? $"{heading}{Environment.NewLine}{Environment.NewLine}{logTail}"
                : $"{heading}{Environment.NewLine}{slotStatus}{Environment.NewLine}{Environment.NewLine}{logTail}";
            _overviewRuntimeLogBox.CaretIndex = _overviewRuntimeLogBox.Text.Length;
            _overviewRuntimeLogBox.ScrollToEnd();
        }
        catch (Exception ex)
        {
            _overviewRuntimeLogBox.Text = $"Could not read runtime log yet.{Environment.NewLine}{Environment.NewLine}{ex.Message}";
        }
    }

    private void ResetMetricCounters()
    {
        _lastMetricRuntimeKey = "";
        _lastPredictedTokenCounter = null;
        _lastPromptTokenCounter = null;
        _lastMetricPollAt = null;
        _lastSlotRuntimeKey = "";
        _lastSlotPromptProcessedCounter = null;
        _lastSlotGeneratedCounter = null;
        _lastSlotPollAt = null;
        _lastRuntimeMetricDisplay = null;
    }
}
