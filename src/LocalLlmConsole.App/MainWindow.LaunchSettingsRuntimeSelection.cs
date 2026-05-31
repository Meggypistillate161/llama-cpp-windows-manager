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
        var runtimeCombo = _launchSettingsPanel.RuntimeCombo;
        if (runtimeCombo?.SelectedItem is RuntimeChoice selected) return selected.Backend;
        if (runtimeCombo?.SelectedValue is string selectedId)
            return _viewModel.LaunchSettings.RuntimeChoices.FirstOrDefault(choice => string.Equals(choice.Id, selectedId, StringComparison.OrdinalIgnoreCase))?.Backend;
        return null;
    }

    private async Task RefreshRuntimeSelectorAsync(string? preferredRuntimeId = null, IReadOnlyList<RuntimeRecord>? runtimes = null)
    {
        if (_launchSettingsPanel.RuntimeCombo is null) return;
        var selectedRuntimeId = preferredRuntimeId ?? SelectedLaunchRuntimeId();
        runtimes ??= await AppServices.StateStore.ListRuntimesAsync();
        var selectorState = _coreServices.Models.LaunchRuntimeSelection.BuildSelectorState(runtimes, selectedRuntimeId);
        _viewModel.LaunchSettings.ApplyRuntimeSelectorState(selectorState);
        SetRuntimeSelection(selectorState.SelectedRuntimeId);
        UpdateLaunchControlVisibility();
    }

    private void SetRuntimeSelection(string? runtimeId)
    {
        var runtimeCombo = _launchSettingsPanel.RuntimeCombo;
        if (runtimeCombo is null) return;
        if (_viewModel.LaunchSettings.RuntimeChoices.Count == 0)
        {
            runtimeCombo.SelectedIndex = -1;
            return;
        }

        var match = _viewModel.LaunchSettings.RuntimeChoices.FirstOrDefault(choice => string.Equals(choice.Id, runtimeId, StringComparison.OrdinalIgnoreCase));
        if (match is null)
            match = _viewModel.LaunchSettings.RuntimeChoices.First();

        runtimeCombo.SelectedValue = match.Id;
    }

    private string SelectedLaunchRuntimeId()
    {
        var runtimeCombo = _launchSettingsPanel.RuntimeCombo;
        if (runtimeCombo?.SelectedValue is string selectedValue) return selectedValue;
        if (runtimeCombo?.SelectedItem is RuntimeChoice choice) return choice.Id;
        return "";
    }

    private bool IsModelLoaded(ModelRecord? model)
        => model is not null && _sessions.SessionForModel(model.Id) is { IsRunning: true };

    private bool IsModelActive(ModelRecord? model)
        => model is not null && _sessions.IsModelActive(model.Id);
}
