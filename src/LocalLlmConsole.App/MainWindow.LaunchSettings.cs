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
    private void ScheduleSelectedModelLaunchSettingsRefresh()
    {
        var previous = _launchSettingsRefreshCts;
        var current = new CancellationTokenSource();
        _launchSettingsRefreshCts = current;
        previous?.Cancel();
        RunBackground(() => RenderSelectedModelLaunchSettingsDebouncedAsync(current), "Launch settings refresh failed");
    }

    private void CancelLaunchSettingsRefresh()
    {
        var cancellation = _launchSettingsRefreshCts;
        _launchSettingsRefreshCts = null;
        cancellation?.Cancel();
    }

    private async Task RenderSelectedModelLaunchSettingsDebouncedAsync(CancellationTokenSource refreshCts)
    {
        try
        {
            await Task.Delay(120, refreshCts.Token);
            await RenderSelectedModelLaunchSettingsAsync(refreshCts.Token);
        }
        finally
        {
            if (ReferenceEquals(_launchSettingsRefreshCts, refreshCts))
                _launchSettingsRefreshCts = null;
            refreshCts.Dispose();
        }
    }

    private async Task RenderSelectedModelLaunchSettingsAsync(CancellationToken cancellationToken = default)
    {
        var model = SelectedModel();
        if (model is null)
        {
            _launchSettingsModelId = "";
            _savedLaunchSettingsSnapshot = null;
            _hasSavedLaunchSettingsSnapshot = false;
            await RefreshRuntimeSelectorAsync(preferredRuntimeId: "");
            cancellationToken.ThrowIfCancellationRequested();
            ApplyLaunchSettingsToControls(_settings);
            await ApplyModelCapabilitiesAsync(null, cancellationToken);
            UpdateLaunchSaveButtonState();
            return;
        }

        var selectedId = model.Id;
        var profile = _stateStore is null ? null : await _stateStore.GetModelLaunchSettingsAsync(selectedId);
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.Equals(SelectedModel()?.Id, selectedId, StringComparison.OrdinalIgnoreCase)) return;

        if (profile is null)
        {
            _savedLaunchSettingsSnapshot = null;
            _hasSavedLaunchSettingsSnapshot = false;
            await RefreshRuntimeSelectorAsync(preferredRuntimeId: "");
            cancellationToken.ThrowIfCancellationRequested();
            ApplyLaunchSettingsToControls(_settings);
            await ApplyModelCapabilitiesAsync(model, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            _launchSettingsModelId = selectedId;
            UpdateLaunchSaveButtonState();
            return;
        }

        _savedLaunchSettingsSnapshot = profile;
        _hasSavedLaunchSettingsSnapshot = true;
        await RefreshRuntimeSelectorAsync(profile.RuntimeId);
        cancellationToken.ThrowIfCancellationRequested();
        ApplyLaunchSettingsToControls(profile.ApplyTo(_settings));
        await ApplyModelCapabilitiesAsync(model, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        _launchSettingsModelId = selectedId;
        UpdateLaunchSaveButtonState();
    }

    private async Task SaveLaunchSettingsForSelectedModelAsync()
    {
        var model = SelectedModel();
        if (model is null) { SetStatus("Select a model before saving launch settings."); return; }
        await RunAsync("Saving model launch profile...", async () =>
        {
            if (!string.Equals(_launchSettingsModelId, model.Id, StringComparison.OrdinalIgnoreCase))
                await RenderSelectedModelLaunchSettingsAsync();
            var launchSettings = ReadLaunchSettingsFromControls();
            await SaveLaunchSettingsForModelAsync(model, launchSettings);
            SetStatus($"Launch profile saved for {model.Name}.");
        });
    }

    private async Task SaveLaunchDefaultsFromControlsAsync()
    {
        await RunAsync("Saving launch defaults...", async () =>
        {
            _settings = ReadLaunchSettingsFromControls();
            await PersistSettingsAsync();
            UpdateLaunchSaveButtonState();
            SetStatus("Launch defaults saved. Runtime choices stay per-model.");
        });
    }

    private async Task SaveLaunchSettingsForModelAsync(ModelRecord model, AppSettings launchSettings)
    {
        Require(_stateStore);
        var saved = ModelLaunchSettings.FromAppSettings(launchSettings, SelectedLaunchRuntimeId());
        await _stateStore!.SaveModelLaunchSettingsAsync(model.Id, saved);
        _savedLaunchSettingsSnapshot = saved;
        _hasSavedLaunchSettingsSnapshot = true;
        _launchSettingsModelId = model.Id;
        UpdateLaunchSaveButtonState();
    }

    private void ResetLaunchSettingsToDefaults()
    {
        var defaults = AppSettings.CreateDefault(_workspaceRoot);
        ApplyLaunchSettingsToControls(ModelLaunchSettings.FromAppSettings(defaults).ApplyTo(_settings));
        UpdateLaunchSaveButtonState();
        SetStatus("Launch settings reset in the form. Save For Model or Save As Default to persist them.");
    }

    private AppSettings ReadLaunchSettingsFromControls()
    {
        var next = _settings with
        {
            ContextSize = ReadContextSize(_contextSizeBox),
            GpuLayers = ReadInt(_gpuLayersBox, "GPU layers", min: 0),
            ParallelSlots = ReadInt(_parallelSlotsBox, "Parallel slots", min: 1),
            BatchSize = ReadInt(_batchSizeBox, "Batch size", min: 1),
            MicroBatchSize = ReadInt(_microBatchSizeBox, "Micro batch size", min: 1),
            Threads = ReadInt(_threadsBox, "Threads", min: 0),
            ReasoningMode = ComboValue(_reasoningCombo),
            ReasoningFormat = ComboValue(_reasoningFormatCombo),
            ReasoningBudget = ReadInt(_reasoningBudgetBox, "Reasoning budget", min: -1),
            VisionMode = ComboValue(_visionCombo),
            VisionImageMinTokens = ReadInt(_visionImageMinTokensBox, "Image min tokens", min: 0),
            VisionImageMaxTokens = ReadInt(_visionImageMaxTokensBox, "Image max tokens", min: 0),
            FlashAttention = ComboValue(_flashAttentionCombo),
            CacheTypeK = ComboValue(_cacheTypeKCombo),
            CacheTypeV = ComboValue(_cacheTypeVCombo),
            KvOffload = ComboValue(_kvOffloadCombo),
            KvUnified = ComboValue(_kvUnifiedCombo),
            ContinuousBatching = ComboValue(_continuousBatchingCombo),
            JinjaMode = ComboValue(_jinjaCombo),
            MmapMode = ComboValue(_mmapCombo),
            MlockMode = ComboValue(_mlockCombo),
            EnableMetrics = ComboValue(_metricsCombo) == "on",
            Temperature = ReadDouble(_temperatureBox, "Temperature", min: 0),
            TopK = ReadInt(_topKBox, "Top K", min: 0),
            TopP = ReadDouble(_topPBox, "Top P", min: 0, max: 1),
            MinP = ReadDouble(_minPBox, "Min P", min: 0, max: 1),
            MaxTokens = ReadInt(_maxTokensBox, "Max tokens", min: -1),
            Seed = ReadInt(_seedBox, "Seed", min: -1),
            RepeatLastN = ReadInt(_repeatLastNBox, "Repeat window", min: -1),
            RepeatPenalty = ReadDouble(_repeatPenaltyBox, "Repeat penalty", min: 0),
            PresencePenalty = ReadDouble(_presencePenaltyBox, "Presence penalty", min: -10, max: 10),
            FrequencyPenalty = ReadDouble(_frequencyPenaltyBox, "Frequency penalty", min: -10, max: 10),
            RopeScaling = ComboValue(_ropeScalingCombo),
            RopeScale = ReadDouble(_ropeScaleBox, "RoPE scale", min: 0),
            RopeFreqBase = ReadDouble(_ropeFreqBaseBox, "RoPE base", min: 0),
            RopeFreqScale = ReadDouble(_ropeFreqScaleBox, "RoPE frequency scale", min: 0),
            SpeculativeType = ComboValue(_speculativeTypeCombo),
            SpecDraftModelPath = _specDraftModelPathBox?.Text.Trim() ?? "",
            SpecDraftGpuLayers = ReadInt(_specDraftGpuLayersBox, "Draft GPU layers", min: -1),
            SpecDraftMinTokens = ReadInt(_specDraftMinTokensBox, "Draft min tokens", min: 0),
            SpecDraftMaxTokens = ReadInt(_specDraftMaxTokensBox, "Draft max tokens", min: 0),
            SpecDraftPSplit = ReadDouble(_specDraftPSplitBox, "Draft split probability", min: -1, max: 1),
            SpecDraftPMin = ReadDouble(_specDraftPMinBox, "Draft min probability", min: -1, max: 1),
            SpecDraftCacheTypeK = ComboValue(_specDraftCacheTypeKCombo),
            SpecDraftCacheTypeV = ComboValue(_specDraftCacheTypeVCombo)
        };
        if (next.SpecDraftPSplit < 0 && Math.Abs(next.SpecDraftPSplit + 1) > 0.000_001)
            throw new InvalidOperationException("Draft split probability must be -1 for default or between 0 and 1.");
        if (next.SpecDraftPMin < 0 && Math.Abs(next.SpecDraftPMin + 1) > 0.000_001)
            throw new InvalidOperationException("Draft min probability must be -1 for default or between 0 and 1.");
        if (next.SpecDraftMaxTokens > 0 && next.SpecDraftMinTokens > next.SpecDraftMaxTokens)
            throw new InvalidOperationException("Draft min tokens cannot be larger than draft max tokens.");
        if (next.VisionImageMaxTokens > 0 && next.VisionImageMinTokens > next.VisionImageMaxTokens)
            throw new InvalidOperationException("Image min tokens cannot be larger than image max tokens.");
        return next;
    }

    private void ApplyLaunchSettingsToControls(AppSettings? source = null)
    {
        _updatingLaunchSettingsControls = true;
        var settings = source ?? _settings;
        SetText(_contextSizeBox, settings.ContextSize);
        SetText(_gpuLayersBox, settings.GpuLayers);
        SetText(_parallelSlotsBox, settings.ParallelSlots);
        SetText(_batchSizeBox, settings.BatchSize);
        SetText(_microBatchSizeBox, settings.MicroBatchSize);
        SetText(_threadsBox, settings.Threads);
        SetText(_reasoningBudgetBox, settings.ReasoningBudget);
        SetText(_visionImageMinTokensBox, settings.VisionImageMinTokens);
        SetText(_visionImageMaxTokensBox, settings.VisionImageMaxTokens);
        SetText(_temperatureBox, settings.Temperature);
        SetText(_topKBox, settings.TopK);
        SetText(_topPBox, settings.TopP);
        SetText(_minPBox, settings.MinP);
        SetText(_maxTokensBox, settings.MaxTokens);
        SetText(_seedBox, settings.Seed);
        SetText(_repeatLastNBox, settings.RepeatLastN);
        SetText(_repeatPenaltyBox, settings.RepeatPenalty);
        SetText(_presencePenaltyBox, settings.PresencePenalty);
        SetText(_frequencyPenaltyBox, settings.FrequencyPenalty);
        SetText(_ropeScaleBox, settings.RopeScale);
        SetText(_ropeFreqBaseBox, settings.RopeFreqBase);
        SetText(_ropeFreqScaleBox, settings.RopeFreqScale);
        SetText(_specDraftModelPathBox, settings.SpecDraftModelPath);
        SetText(_specDraftGpuLayersBox, settings.SpecDraftGpuLayers);
        SetText(_specDraftMinTokensBox, settings.SpecDraftMinTokens);
        SetText(_specDraftMaxTokensBox, settings.SpecDraftMaxTokens);
        SetText(_specDraftPSplitBox, settings.SpecDraftPSplit);
        SetText(_specDraftPMinBox, settings.SpecDraftPMin);
        SetCombo(_metricsCombo, settings.EnableMetrics ? "on" : "off");
        SetCombo(_reasoningCombo, settings.ReasoningMode);
        SetCombo(_reasoningFormatCombo, settings.ReasoningFormat);
        SetCombo(_visionCombo, settings.VisionMode);
        SetCombo(_flashAttentionCombo, settings.FlashAttention);
        SetCombo(_cacheTypeKCombo, settings.CacheTypeK);
        SetCombo(_cacheTypeVCombo, settings.CacheTypeV);
        SetCombo(_kvOffloadCombo, settings.KvOffload);
        SetCombo(_kvUnifiedCombo, settings.KvUnified);
        SetCombo(_continuousBatchingCombo, settings.ContinuousBatching);
        SetCombo(_jinjaCombo, settings.JinjaMode);
        SetCombo(_mmapCombo, settings.MmapMode);
        SetCombo(_mlockCombo, settings.MlockMode);
        SetCombo(_ropeScalingCombo, settings.RopeScaling);
        SetCombo(_speculativeTypeCombo, settings.SpeculativeType);
        SetCombo(_specDraftCacheTypeKCombo, settings.SpecDraftCacheTypeK);
        SetCombo(_specDraftCacheTypeVCombo, settings.SpecDraftCacheTypeV);
        _updatingLaunchSettingsControls = false;
        UpdateLaunchControlVisibility();
        UpdateLaunchSaveButtonState();
    }

    private void AttachLaunchSettingsChangeHandlers()
    {
        void Changed(object? _, EventArgs __)
        {
            if (!_updatingLaunchSettingsControls)
            {
                UpdateContextSizeSuggestion();
                UpdateSpeculativeControlsEnabled();
                UpdateLaunchSaveButtonState();
            }
        }

        if (_contextSizeBox is not null)
            _contextSizeBox.LostFocus += (_, _) => NormalizeContextSizeBox();

        foreach (var box in new[]
        {
            _contextSizeBox, _gpuLayersBox, _parallelSlotsBox, _batchSizeBox, _microBatchSizeBox,
            _threadsBox, _reasoningBudgetBox, _visionImageMinTokensBox, _visionImageMaxTokensBox,
            _temperatureBox, _topKBox, _topPBox, _minPBox,
            _maxTokensBox, _seedBox, _repeatLastNBox, _repeatPenaltyBox, _presencePenaltyBox,
            _frequencyPenaltyBox, _ropeScaleBox, _ropeFreqBaseBox, _ropeFreqScaleBox,
            _specDraftModelPathBox, _specDraftGpuLayersBox, _specDraftMinTokensBox,
            _specDraftMaxTokensBox, _specDraftPSplitBox, _specDraftPMinBox
        }.Where(box => box is not null))
        {
            box!.TextChanged += Changed;
        }

        foreach (var combo in new[]
        {
            _metricsCombo, _reasoningCombo, _reasoningFormatCombo, _visionCombo, _flashAttentionCombo,
            _cacheTypeKCombo, _cacheTypeVCombo, _kvOffloadCombo, _kvUnifiedCombo, _continuousBatchingCombo,
            _jinjaCombo, _mmapCombo, _mlockCombo, _ropeScalingCombo, _speculativeTypeCombo,
            _specDraftCacheTypeKCombo, _specDraftCacheTypeVCombo
        }.Where(combo => combo is not null))
        {
            combo!.SelectionChanged += Changed;
        }
    }

    private void UpdateLaunchSaveButtonState()
    {
        if (_saveModelLaunchSettingsButton is null) return;
        if (SelectedModel() is null)
        {
            _saveModelLaunchSettingsButton.Content = "Save For Model";
            _saveModelLaunchSettingsButton.IsEnabled = false;
            return;
        }

        if (!_hasSavedLaunchSettingsSnapshot || _savedLaunchSettingsSnapshot is null)
        {
            _saveModelLaunchSettingsButton.Content = "Save For Model";
            _saveModelLaunchSettingsButton.IsEnabled = true;
            return;
        }

        if (!TryReadCurrentModelLaunchSettings(out var current))
        {
            _saveModelLaunchSettingsButton.Content = "Save For Model";
            _saveModelLaunchSettingsButton.IsEnabled = true;
            return;
        }

        var saved = Equals(current, _savedLaunchSettingsSnapshot);
        _saveModelLaunchSettingsButton.Content = saved ? "Saved" : "Save For Model";
        _saveModelLaunchSettingsButton.IsEnabled = !saved;
    }

    private bool TryReadCurrentModelLaunchSettings(out ModelLaunchSettings? current)
    {
        current = null;
        try
        {
            current = ModelLaunchSettings.FromAppSettings(ReadLaunchSettingsFromControls(), SelectedLaunchRuntimeId());
            return true;
        }
        catch
        {
            return false;
        }
    }

}
