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
    private async Task RefreshOpenCodeAsync(string preferredModelId = "", string preferredAgentId = "")
    {
        Require(_openCode);
        preferredModelId = string.IsNullOrWhiteSpace(preferredModelId) ? SelectedOpenCodeModel()?.FullId ?? "" : preferredModelId;
        preferredAgentId = string.IsNullOrWhiteSpace(preferredAgentId) ? SelectedOpenCodeAgent()?.Id ?? "" : preferredAgentId;

        UpdateOpenCodePathText();

        _viewModel.OpenCode.ReplaceLocalModels(_stateStore is null ? [] : await _stateStore.ListModelsAsync());
        if (_openCodeLocalModelCombo is not null && _viewModel.OpenCode.LocalModelChoices.Count > 0)
            _openCodeLocalModelCombo.SelectedIndex = 0;

        _viewModel.OpenCode.ReplaceModels(_openCode!.ListModels(_openCodeFiles.ConfigPath));
        if (_openCodeModelCombo is not null)
        {
            _openCodeModelCombo.SelectedItem = _viewModel.OpenCode.ModelChoices.FirstOrDefault(model => string.Equals(model.FullId, preferredModelId, StringComparison.OrdinalIgnoreCase))
                ?? _viewModel.OpenCode.ModelChoices.FirstOrDefault(model => !model.IsAddNew)
                ?? _viewModel.OpenCode.ModelChoices.FirstOrDefault();
        }

        _viewModel.OpenCode.ReplaceAgents(_openCode.ListAgents(_openCodeFiles.ConfigPath, _openCodeFiles.AgentsDirectory));
        if (_openCodeAgentCombo is not null)
        {
            _openCodeAgentCombo.SelectedItem = _viewModel.OpenCode.AgentChoices.FirstOrDefault(agent => string.Equals(agent.Id, preferredAgentId, StringComparison.OrdinalIgnoreCase))
                ?? _viewModel.OpenCode.AgentChoices.FirstOrDefault(agent => !agent.IsAddNew)
                ?? _viewModel.OpenCode.AgentChoices.FirstOrDefault();
        }

        await LoadSelectedOpenCodeModelAsync();
        await LoadSelectedOpenCodeAgentAsync();
    }

    private async Task LoadSelectedOpenCodeModelAsync()
    {
        await Task.CompletedTask;
        var model = SelectedOpenCodeModel();
        var adding = model?.IsAddNew ?? true;
        if (_openCodeAddModelPanel is not null) _openCodeAddModelPanel.Visibility = adding ? Visibility.Visible : Visibility.Collapsed;
        if (_openCodeDeleteModelButton is not null) _openCodeDeleteModelButton.Visibility = adding ? Visibility.Collapsed : Visibility.Visible;

        if (_openCodeModelSnippetBox is null) return;
        if (adding)
        {
            _openCodeSelectedModelSnapshot = "";
            UpdateOpenCodeModelEditorState();
            await LoadOpenCodeLocalModelDraftAsync();
            return;
        }

        try
        {
            Require(_openCode);
            var snippet = _openCode!.ReadModelSnippet(_openCodeFiles.ConfigPath, model!);
            _openCodeSelectedModelSnapshot = snippet;
            _updatingOpenCodeModelEditor = true;
            _openCodeModelSnippetBox.Text = snippet;
            _updatingOpenCodeModelEditor = false;
            UpdateOpenCodeModelEditorState();
        }
        catch (Exception ex)
        {
            _openCodeSelectedModelSnapshot = "";
            _updatingOpenCodeModelEditor = true;
            _openCodeModelSnippetBox.Text = ex.Message;
            _updatingOpenCodeModelEditor = false;
            UpdateOpenCodeModelEditorState();
            SetStatus(ex.Message);
        }
    }

    private async Task LoadSelectedOpenCodeAgentAsync()
    {
        await Task.CompletedTask;
        var agent = SelectedOpenCodeAgent();
        var adding = agent?.IsAddNew ?? true;
        if (_openCodeAddAgentPanel is not null) _openCodeAddAgentPanel.Visibility = adding ? Visibility.Visible : Visibility.Collapsed;
        if (_openCodeSaveAgentButton is not null) _openCodeSaveAgentButton.IsEnabled = !adding;
        if (_openCodeDeleteAgentButton is not null) _openCodeDeleteAgentButton.IsEnabled = !adding;
        if (_openCodeCreateAgentButton is not null) _openCodeCreateAgentButton.IsEnabled = true;

        if (_openCodeAgentSnippetBox is null) return;
        if (adding)
        {
            _openCodeAgentSnippetBox.Text = "";
            return;
        }

        try
        {
            Require(_openCode);
            _openCodeAgentSnippetBox.Text = _openCode!.ReadAgentSnippet(_openCodeFiles.ConfigPath, agent!);
        }
        catch (Exception ex)
        {
            _openCodeAgentSnippetBox.Text = ex.Message;
            SetStatus(ex.Message);
        }
    }

    private async Task SaveOpenCodeModelSnippetAsync()
    {
        var model = SelectedOpenCodeModel();
        if (model is null || model.IsAddNew) { SetStatus("Choose an OpenCode model first."); return; }
        Require(_openCode);
        var snippet = _openCodeModelSnippetBox?.Text ?? "";
        if (_openCode!.SnippetsEquivalent(_openCodeSelectedModelSnapshot, snippet))
        {
            UpdateOpenCodeModelEditorState();
            SetStatus($"OpenCode model {model.FullId} is already saved.");
            return;
        }

        await RunAsync("Saving OpenCode model snippet...", async () =>
        {
            _openCode.SaveModelSnippet(_openCodeFiles.ConfigPath, model, snippet);
            await RefreshOpenCodeAsync(preferredModelId: model.FullId);
            SetStatus($"Saved OpenCode model {model.FullId}.");
        });
    }

    private async Task DeleteOpenCodeModelAsync()
    {
        var model = SelectedOpenCodeModel();
        if (model is null || model.IsAddNew) { SetStatus("Choose an OpenCode model first."); return; }
        if (ThemedMessageBox.Show(this, $"Delete this OpenCode model config?\n\n{model.Label}", "Delete OpenCode model", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        await RunAsync("Deleting OpenCode model config...", async () =>
        {
            Require(_openCode);
            _openCode!.DeleteModel(_openCodeFiles.ConfigPath, model);
            await RefreshOpenCodeAsync();
            SetStatus($"Deleted OpenCode model {model.FullId}.");
        });
    }

    private OpenCodeModelEntry? SelectedOpenCodeModel() => _openCodeModelCombo?.SelectedItem as OpenCodeModelEntry;
    private OpenCodeAgentEntry? SelectedOpenCodeAgent() => _openCodeAgentCombo?.SelectedItem as OpenCodeAgentEntry;
}
