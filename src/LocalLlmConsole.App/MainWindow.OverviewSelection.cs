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
    private async Task RefreshOverviewModelSelectorAsync()
    {
        if (_stateStore is null) return;
        RefreshOverviewModelChoices(await _stateStore.ListModelsAsync());
    }

    private void RefreshOverviewModelChoices(IReadOnlyList<ModelRecord> models)
    {
        var selectedId = SelectedOverviewModel()?.Id;
        if (string.IsNullOrWhiteSpace(selectedId))
            selectedId = _sessions.SelectedSnapshot()?.ModelId;

        _viewModel.Overview.ReplaceModels(models);

        if (_overviewModelCombo is not null)
        {
            if (_viewModel.Overview.ModelChoices.Count == 0)
            {
                _overviewModelCombo.SelectedIndex = -1;
            }
            else
            {
                var match = _viewModel.Overview.ModelChoices.FirstOrDefault(model => string.Equals(model.Id, selectedId, StringComparison.OrdinalIgnoreCase))
                    ?? _viewModel.Overview.ModelChoices.First();
                _overviewModelCombo.SelectedValue = match.Id;
            }
        }

        UpdateOverviewModelActions();
    }

    private ModelRecord? SelectedOverviewModel()
    {
        if (_overviewModelCombo?.SelectedItem is ModelRecord model) return model;
        if (_overviewModelCombo?.SelectedValue is string selectedId)
            return _viewModel.Overview.ModelChoices.FirstOrDefault(item => string.Equals(item.Id, selectedId, StringComparison.OrdinalIgnoreCase));
        return null;
    }

    private void UpdateOverviewModelActions()
    {
        var model = SelectedOverviewModel();
        var hasSelection = model is not null;
        var selectedModelLoaded = IsModelLoaded(model);
        if (_overviewLoadButton is not null) _overviewLoadButton.IsEnabled = hasSelection && !selectedModelLoaded;
        if (_overviewUnloadButton is not null) _overviewUnloadButton.IsEnabled = selectedModelLoaded;
    }

    private async Task SelectOverviewModelSessionAsync()
    {
        var model = SelectedOverviewModel();
        if (model is null) return;

        var selectedLoadedModel = IsModelLoaded(model);
        if (selectedLoadedModel && !IsModelActive(model))
        {
            _sessions.SelectModel(model.Id);
            _activeRuntimeSettings = _sessions.ActiveSettings;
            await SaveActiveRuntimeSessionsAsync();
        }

        ResetMetricCounters();
        if (!selectedLoadedModel)
            SetStatus($"{model.Name} is not loaded. Load it to expose an OpenAI-compatible endpoint.");
        await RefreshRuntimeMetricsAsync();
    }

    private async Task<(string Model, string Runtime)> ActiveRuntimeLabelsAsync()
    {
        await Task.CompletedTask;
        var selectedModel = SelectedOverviewModel();
        var active = selectedModel is null
            ? _sessions.SelectedSnapshot()
            : _sessions.SessionForModel(selectedModel.Id);
        if (selectedModel is not null && active is null)
            return ($"Stopped: {selectedModel.Name}", "No loaded runtime");
        if (active is null) return ("None", "Stopped");
        var status = active.Status switch
        {
            LoadedModelSessionStatus.Running => "Loaded",
            LoadedModelSessionStatus.Warm => "Loaded",
            LoadedModelSessionStatus.Loading => "Loading",
            LoadedModelSessionStatus.Failed => "Failed",
            _ => "Stopped"
        };
        if (_llama.State == LlamaRuntimeState.Failed && _llama.LastExitCode is int exitCode)
            status = $"Failed ({exitCode})";
        return ($"{status}: {active.ModelName}", string.IsNullOrWhiteSpace(active.RuntimeName) ? "Unknown runtime" : active.RuntimeName);
    }

    private async Task<string> ActiveModelDisplayNameAsync(string modelId)
    {
        var model = modelId;
        if (_stateStore is not null)
        {
            var models = await _stateStore.ListModelsAsync();
            model = models.FirstOrDefault(item => string.Equals(item.Id, modelId, StringComparison.OrdinalIgnoreCase))?.Name ?? model;
        }

        return string.IsNullOrWhiteSpace(model) ? "Unknown model" : model;
    }
}
