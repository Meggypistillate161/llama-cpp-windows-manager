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
    private async Task LoadOpenCodeLocalModelDraftAsync()
    {
        await _coreServices.OpenCodeServices.OpenCodeLocalModelApplication.LoadDraftAsync(
            new OpenCodeLocalModelDraftLoadRequest(
                SelectedOpenCodeModel()?.IsAddNew == true,
                _openCodePage.ModelSnippetBox is not null,
                _openCodePage.SelectedLocalModel,
                _openCodeFileSet.Current,
                _settings,
                _openCodePage.SelectedLocalModel is { } selectedModel ? _sessions.SessionForModel(selectedModel.Id) : null,
                UseOpenCodeGatewayProvider()),
            new OpenCodeLocalModelDraftLoadActions(
                () => _openCodePage.SelectedLocalModel,
                async (profileModel, _) => await EnsureModelLaunchProfileAsync(profileModel),
                async (settings, _) => await EnsureModelApiKeyAsync(settings),
                ReadOpenCodeModelCapabilitiesAsync,
                OpenCodeLocalModelDraftActions()));
    }

    private OpenCodeLocalModelDraftApplicationActions OpenCodeLocalModelDraftActions()
        => new(
            SetOpenCodeModelSnippetText,
            UpdateOpenCodeModelEditorState,
            SetOpenCodeAddModelStatus,
            ApplyOpenCodeLocalModelActionState,
            UpdateOpenCodeExistingModelButtons);

    private OpenCodeLocalModelSaveApplicationActions OpenCodeLocalModelSaveActions()
        => new(
            preferredModelId => RefreshOpenCodeAsync(preferredModelId: preferredModelId),
            SetStatus);

    private OpenCodeLocalModelSnippetSaveActions OpenCodeLocalModelSnippetSaveActions()
        => new(
            async (profileModel, _) => await EnsureModelLaunchProfileAsync(profileModel),
            async (settings, _) => await EnsureModelApiKeyAsync(settings),
            OpenCodeLocalModelSaveActions());

    private async ValueTask<ModelCapabilitySummary> ReadOpenCodeModelCapabilitiesAsync(ModelRecord model, CancellationToken cancellationToken)
        => await CachedModelCapabilitiesAsync(model, cancellationToken);

    private async Task<OpenCodeModelLimits> ResolveOpenCodeModelLimitsAsync(ModelRecord model, AppSettings launchSettings, CancellationToken cancellationToken = default)
        => await _coreServices.OpenCodeServices.OpenCodeLocalModelApplication.ResolveLimitsAsync(model, launchSettings, ReadOpenCodeModelCapabilitiesAsync, cancellationToken);

    private void UpdateOpenCodeModelEditorState()
    {
        var state = _coreServices.OpenCodeServices.OpenCodeModelApplication.EditorState(new OpenCodeModelEditorStateApplicationRequest(
            SelectedOpenCodeModel(),
            _openCodePage.ModelSnippet,
            _openCodeModelEditor.SavedSnippet));
        ApplyOpenCodeExistingModelButtons(state.ExistingModelState);
        if (state.RefreshLocalModelAddState)
            UpdateOpenCodeLocalModelAddState();
    }

    private void UpdateOpenCodeExistingModelButtons(bool valid, bool matchesSaved)
        => ApplyOpenCodeExistingModelButtons(_coreServices.OpenCodeServices.OpenCodeModelApplication.ExistingModelEditorState(SelectedOpenCodeModel(), valid, matchesSaved));

    private void ApplyOpenCodeExistingModelButtons(OpenCodeExistingModelEditorState state)
    {
        if (_openCodePage.SaveModelButton is not null)
        {
            _openCodePage.SaveModelButton.Content = state.SaveContent;
            _openCodePage.SaveModelButton.Visibility = state.SaveVisible ? Visibility.Visible : Visibility.Collapsed;
            _openCodePage.SaveModelButton.IsEnabled = state.SaveEnabled;
        }
        if (_openCodePage.DeleteModelButton is not null)
        {
            _openCodePage.DeleteModelButton.Visibility = state.DeleteVisible ? Visibility.Visible : Visibility.Collapsed;
            _openCodePage.DeleteModelButton.IsEnabled = state.DeleteEnabled;
        }
    }

    private void UpdateOpenCodeLocalModelAddState()
    {
        _coreServices.OpenCodeServices.OpenCodeLocalModelApplication.UpdateAddState(
            new OpenCodeLocalModelAddStateRequest(
                SelectedOpenCodeModel()?.IsAddNew == true,
                _openCodeFileSet.ConfigPath,
                _openCodePage.SelectedLocalModel,
                _openCodePage.ModelSnippet,
                UseOpenCodeGatewayProvider()),
            new OpenCodeLocalModelAddStateActions(ApplyOpenCodeLocalModelActionState));
    }

    private void ApplyOpenCodeLocalModelActionState(OpenCodeLocalModelActionState state)
    {
        SetOpenCodeAddModelStatus(state.Status);
        if (_openCodePage.AddLocalModelButton is not null)
        {
            _openCodePage.AddLocalModelButton.Visibility = state.AddVisible ? Visibility.Visible : Visibility.Collapsed;
            _openCodePage.AddLocalModelButton.IsEnabled = state.AddEnabled;
        }
        if (_openCodePage.UpdateLocalModelButton is not null)
        {
            _openCodePage.UpdateLocalModelButton.Visibility = state.UpdateVisible ? Visibility.Visible : Visibility.Collapsed;
            _openCodePage.UpdateLocalModelButton.IsEnabled = state.UpdateEnabled;
        }
        if (_openCodePage.AddAsNewLocalModelButton is not null)
        {
            _openCodePage.AddAsNewLocalModelButton.Visibility = state.AddAsNewVisible ? Visibility.Visible : Visibility.Collapsed;
            _openCodePage.AddAsNewLocalModelButton.IsEnabled = state.AddAsNewEnabled;
        }
    }

    private void SetOpenCodeAddModelStatus(string text)
    {
        if (_openCodePage.AddModelStatusText is not null)
            _openCodePage.AddModelStatusText.Text = text;
    }

    private void SetOpenCodeModelSnippetText(string text)
    {
        if (_openCodePage.ModelSnippetBox is null) return;
        _openCodeModelEditor.RunProgrammaticUpdate(() => _openCodePage.ModelSnippetBox.Text = text);
    }

    private async Task SaveOpenCodeLocalModelSnippetAsync(bool addAsNew)
    {
        var selectedLocalModel = _openCodePage.SelectedLocalModel;
        var request = new OpenCodeLocalModelSnippetSaveRequest(
            selectedLocalModel,
            _openCodeFileSet.Current,
            _settings,
            selectedLocalModel is not null ? _sessions.SessionForModel(selectedLocalModel.Id) : null,
            _openCodePage.ModelSnippet,
            addAsNew,
            UseOpenCodeGatewayProvider());

        if (request.SelectedLocalModel is null)
        {
            await _coreServices.OpenCodeServices.OpenCodeLocalModelApplication.SaveSnippetAsync(request, OpenCodeLocalModelSnippetSaveActions());
            return;
        }

        await RunAsync(addAsNew ? "Adding local model as a new OpenCode model..." : "Saving local model to OpenCode...", async () =>
        {
            await _coreServices.OpenCodeServices.OpenCodeLocalModelApplication.SaveSnippetAsync(request, OpenCodeLocalModelSnippetSaveActions());
        });
    }

    private bool UseOpenCodeGatewayProvider()
        => _settings.AutoLoadGatewayEnabled;
}
