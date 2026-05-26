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
        if (string.IsNullOrWhiteSpace(selectedId) && _llama.IsRunning)
            selectedId = _llama.ActiveModelId;

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
        var selectedModelActive = IsModelActive(model);
        if (_overviewLoadButton is not null) _overviewLoadButton.IsEnabled = hasSelection && !selectedModelActive;
        if (_overviewUnloadButton is not null) _overviewUnloadButton.IsEnabled = selectedModelActive;
    }

    private async Task<(string Model, string Runtime)> ActiveRuntimeLabelsAsync()
    {
        if (!_llama.IsRunning && _llama.State != LlamaRuntimeState.Failed) return ("None", "Stopped");
        var model = await ActiveModelDisplayNameAsync(_llama.ActiveModelId);
        var runtime = _llama.ActiveRuntimeId;

        if (_stateStore is not null)
        {
            var runtimes = await _stateStore.ListRuntimesAsync();
            runtime = runtimes.FirstOrDefault(item => string.Equals(item.Id, _llama.ActiveRuntimeId, StringComparison.OrdinalIgnoreCase))?.Name ?? runtime;
        }

        model = string.IsNullOrWhiteSpace(model) ? "Unknown model" : model;
        var status = _llama.State switch
        {
            LlamaRuntimeState.Loaded => "Loaded",
            LlamaRuntimeState.Loading => "Loading",
            LlamaRuntimeState.Failed => "Failed",
            _ => "Stopped"
        };
        if (_llama.State == LlamaRuntimeState.Failed && _llama.LastExitCode is int exitCode)
            status = $"Failed ({exitCode})";
        return ($"{status}: {model}", string.IsNullOrWhiteSpace(runtime) ? "Unknown runtime" : runtime);
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
