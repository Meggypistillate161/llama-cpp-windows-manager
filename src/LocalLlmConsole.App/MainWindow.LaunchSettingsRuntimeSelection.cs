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
    private RuntimeBackend? SelectedLaunchRuntimeBackend()
    {
        if (_runtimeCombo?.SelectedItem is RuntimeChoice selected) return selected.Backend;
        if (_runtimeCombo?.SelectedValue is string selectedId)
            return _viewModel.LaunchSettings.RuntimeChoices.FirstOrDefault(choice => string.Equals(choice.Id, selectedId, StringComparison.OrdinalIgnoreCase))?.Backend;
        return null;
    }

    private async Task RefreshRuntimeSelectorAsync(string? preferredRuntimeId = null, IReadOnlyList<RuntimeRecord>? runtimes = null)
    {
        if (_runtimeCombo is null || _stateStore is null) return;
        var selectedRuntimeId = preferredRuntimeId ?? SelectedLaunchRuntimeId();
        runtimes ??= await _stateStore.ListRuntimesAsync();
        _viewModel.LaunchSettings.ReplaceRuntimeChoices(runtimes);
        SetRuntimeSelection(selectedRuntimeId);
        UpdateLaunchControlVisibility();
    }

    private void SetRuntimeSelection(string? runtimeId)
    {
        if (_runtimeCombo is null) return;
        if (_viewModel.LaunchSettings.RuntimeChoices.Count == 0)
        {
            _runtimeCombo.SelectedIndex = -1;
            return;
        }

        var match = _viewModel.LaunchSettings.RuntimeChoices.FirstOrDefault(choice => string.Equals(choice.Id, runtimeId, StringComparison.OrdinalIgnoreCase))
            ?? _viewModel.LaunchSettings.RuntimeChoices.First();
        _runtimeCombo.SelectedValue = match.Id;
    }

    private string SelectedLaunchRuntimeId()
    {
        if (_runtimeCombo?.SelectedValue is string selectedValue) return selectedValue;
        if (_runtimeCombo?.SelectedItem is RuntimeChoice choice) return choice.Id;
        return "";
    }

    private RuntimeRecord? ResolveLaunchRuntime(IReadOnlyList<RuntimeRecord> runtimes)
    {
        var selectedRuntimeId = SelectedLaunchRuntimeId();
        return runtimes.FirstOrDefault(runtime => string.Equals(runtime.Id, selectedRuntimeId, StringComparison.OrdinalIgnoreCase))
            ?? SelectedRuntime()
            ?? runtimes.FirstOrDefault();
    }

    private static RuntimeRecord? ResolveLaunchRuntime(IReadOnlyList<RuntimeRecord> runtimes, string? runtimeId)
        => runtimes.FirstOrDefault(runtime => string.Equals(runtime.Id, runtimeId, StringComparison.OrdinalIgnoreCase))
            ?? runtimes.FirstOrDefault();

    private void UpdateModelActionButtons()
    {
        var model = SelectedModel();
        var hasSelection = model is not null;
        var selectedModelLoaded = IsModelLoaded(model);
        var selectedModelActive = IsModelActive(model);
        if (_loadModelButton is not null) _loadModelButton.IsEnabled = hasSelection && !selectedModelActive;
        if (_restartModelButton is not null) _restartModelButton.IsEnabled = selectedModelLoaded;
        if (_unloadModelButton is not null) _unloadModelButton.IsEnabled = selectedModelActive;
    }

    private bool IsModelLoaded(ModelRecord? model)
        => model is not null
            && _llama.IsRunning
            && _llama.State == LlamaRuntimeState.Loaded
            && string.Equals(_llama.ActiveModelId, model.Id, StringComparison.OrdinalIgnoreCase);

    private bool IsModelActive(ModelRecord? model)
        => model is not null
            && _llama.IsRunning
            && string.Equals(_llama.ActiveModelId, model.Id, StringComparison.OrdinalIgnoreCase);
}
