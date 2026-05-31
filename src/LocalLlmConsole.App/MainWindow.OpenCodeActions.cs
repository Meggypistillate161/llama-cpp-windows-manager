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
        await _coreServices.OpenCodeServices.OpenCodeRefreshApplication.RefreshAsync(
            new OpenCodeRefreshApplicationRequest(
                _openCodeFileSet.Current,
                _settings,
                preferredModelId,
                preferredAgentId,
                SelectedOpenCodeModel()?.FullId ?? "",
                SelectedOpenCodeAgent()?.Id ?? "",
                _openCodePage.HealthText is not null),
            OpenCodeRefreshActions());
    }

    private async Task LoadSelectedOpenCodeModelAsync()
    {
        await _coreServices.OpenCodeServices.OpenCodeModelApplication.LoadSelectedAsync(
            new OpenCodeModelLoadApplicationRequest(
                _openCodeFileSet.Current,
                SelectedOpenCodeModel(),
                _openCodePage.ModelSnippetBox is not null),
            OpenCodeModelLoadSelectedActions());
    }

    private async Task LoadSelectedOpenCodeAgentAsync()
    {
        await _coreServices.OpenCodeServices.OpenCodeAgentApplication.LoadSelectedAsync(
            new OpenCodeAgentLoadApplicationRequest(
                _openCodeFileSet.Current,
                SelectedOpenCodeAgent(),
                _openCodePage.AgentSnippetBox is not null),
            OpenCodeAgentLoadActions());
    }

    private OpenCodeChoicesApplicationActions OpenCodeChoicesActions()
        => new(
            _viewModel.OpenCode.ReplaceChoices,
            () =>
            {
                if (_openCodePage.LocalModelCombo is not null)
                    _openCodePage.LocalModelCombo.SelectedIndex = 0;
            },
            model =>
            {
                if (_openCodePage.ModelCombo is not null)
                    _openCodePage.ModelCombo.SelectedItem = model;
            },
            agent =>
            {
                if (_openCodePage.AgentCombo is not null)
                    _openCodePage.AgentCombo.SelectedItem = agent;
            });

    private OpenCodeModelLoadApplicationActions OpenCodeModelLoadActions()
        => new(
            visible =>
            {
                if (_openCodePage.AddModelPanel is not null)
                    _openCodePage.AddModelPanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            },
            visible =>
            {
                if (_openCodePage.DeleteModelButton is not null)
                    _openCodePage.DeleteModelButton.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            },
            _openCodeModelEditor.ClearSavedSnippet,
            _openCodeModelEditor.SetSavedSnippet,
            SetOpenCodeModelSnippetText,
            UpdateOpenCodeModelEditorState,
            SetStatus);

    private OpenCodeModelLoadSelectedApplicationActions OpenCodeModelLoadSelectedActions()
        => new(LoadOpenCodeLocalModelDraftAsync, OpenCodeModelLoadActions());

    private OpenCodeAgentLoadApplicationActions OpenCodeAgentLoadActions()
        => new(
            ApplyOpenCodeAgentEditorState,
            SetOpenCodeAgentSnippetText,
            SetStatus);

    private void ApplyOpenCodeAgentEditorState(OpenCodeAgentEditorState state)
    {
        if (_openCodePage.AddAgentPanel is not null) _openCodePage.AddAgentPanel.Visibility = state.AddPanelVisible ? Visibility.Visible : Visibility.Collapsed;
        if (_openCodePage.SaveAgentButton is not null) _openCodePage.SaveAgentButton.IsEnabled = state.SaveEnabled;
        if (_openCodePage.DeleteAgentButton is not null) _openCodePage.DeleteAgentButton.IsEnabled = state.DeleteEnabled;
        if (_openCodePage.CreateAgentButton is not null) _openCodePage.CreateAgentButton.IsEnabled = state.CreateEnabled;
    }

    private void SetOpenCodeAgentSnippetText(string text)
    {
        if (_openCodePage.AgentSnippetBox is not null)
            _openCodePage.AgentSnippetBox.Text = text;
    }

    private async Task SaveOpenCodeModelSnippetAsync()
    {
        await _coreServices.OpenCodeServices.OpenCodeModelApplication.SaveSnippetAsync(
            new OpenCodeModelSaveApplicationRequest(
                _openCodeFileSet.Current,
                SelectedOpenCodeModel(),
                _openCodePage.ModelSnippet,
                _openCodeModelEditor.SavedSnippet),
            OpenCodeModelSaveActions());
    }

    private async Task DeleteOpenCodeModelAsync()
    {
        await _coreServices.OpenCodeServices.OpenCodeModelApplication.DeleteAsync(
            new OpenCodeModelDeleteApplicationRequest(
                _openCodeFileSet.Current,
                SelectedOpenCodeModel()),
            OpenCodeModelDeleteActions());
    }

    private OpenCodeModelEntry? SelectedOpenCodeModel() => _openCodePage.SelectedModel;
    private OpenCodeAgentEntry? SelectedOpenCodeAgent() => _openCodePage.SelectedAgent;

    private OpenCodeHealthApplicationActions OpenCodeHealthActions()
        => new(
            summary =>
            {
                if (_openCodePage.HealthText is not null)
                    _openCodePage.HealthText.Text = summary;
            },
            detail =>
            {
                if (_openCodePage.HealthText is not null)
                    _openCodePage.HealthText.ToolTip = TooltipText(detail);
            },
            resourceKey =>
            {
                if (_openCodePage.HealthText is not null)
                    _openCodePage.HealthText.Foreground = (System.Windows.Media.Brush)System.Windows.Application.Current.Resources[resourceKey];
            });

    private OpenCodeRefreshApplicationActions OpenCodeRefreshActions()
        => new(
            ListOpenCodeLocalModelsAsync,
            LoadSelectedOpenCodeModelAsync,
            LoadSelectedOpenCodeAgentAsync,
            OpenCodePathActions(),
            OpenCodeHealthActions(),
            OpenCodeChoicesActions());

    private async Task<IReadOnlyList<ModelRecord>> ListOpenCodeLocalModelsAsync()
        => AppServices.ModelLookupApplication is null ? [] : await AppServices.ModelLookupApplication.ListAsync();

    private async Task UpdateOpenCodeHealthAsync()
    {
        if (_openCodePage.HealthText is null) return;
        await _coreServices.OpenCodeServices.OpenCodeRefreshApplication.RefreshHealthAsync(
            _openCodeFileSet.Current,
            _settings,
            ListOpenCodeLocalModelsAsync,
            OpenCodeHealthActions());
    }

    private OpenCodeModelCommandApplicationActions OpenCodeModelCommandActions()
        => new(
            UpdateOpenCodeModelEditorState,
            preferredModelId => RefreshOpenCodeAsync(preferredModelId: preferredModelId),
            () => RefreshOpenCodeAsync(),
            SetStatus);

    private OpenCodeModelSaveApplicationActions OpenCodeModelSaveActions()
        => new(
            RunAsync,
            ConfirmOpenCodeCommand,
            OpenCodeModelCommandActions());

    private OpenCodeModelDeleteApplicationActions OpenCodeModelDeleteActions()
        => new(
            RunAsync,
            ConfirmOpenCodeCommand,
            OpenCodeModelCommandActions());

    private bool ConfirmOpenCodeCommand(OpenCodeCommandConfirmation confirmation)
        => _coreServices.App.Dialogs.Confirm(this, confirmation.Message, confirmation.Title, MessageBoxImage.Warning);
}
