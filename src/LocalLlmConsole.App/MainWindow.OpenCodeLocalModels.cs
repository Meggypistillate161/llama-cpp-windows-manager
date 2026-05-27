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
        if (SelectedOpenCodeModel()?.IsAddNew != true) return;
        if (_openCodeModelSnippetBox is null) return;
        if (_openCodeLocalModelCombo?.SelectedItem is not ModelRecord model)
        {
            _updatingOpenCodeModelEditor = true;
            _openCodeModelSnippetBox.Text = "";
            _updatingOpenCodeModelEditor = false;
            SetOpenCodeAddModelStatus("Choose a local model to add.");
            UpdateOpenCodeLocalModelActionButtons(valid: false, sameIdExists: false, sameConfig: false);
            UpdateOpenCodeExistingModelButtons(valid: false, matchesSaved: true);
            return;
        }

        try
        {
            Require(_openCode);
            var selectedModelId = model.Id;
            var launchSettings = await OpenCodeLocalModelLaunchSettingsAsync(model);
            var (contextSize, outputLimit) = await OpenCodeModelLimitsAsync(model, launchSettings);
            if (_openCodeLocalModelCombo?.SelectedItem is not ModelRecord selected
                || !string.Equals(selected.Id, selectedModelId, StringComparison.OrdinalIgnoreCase))
                return;

            var draft = _openCode!.CreateLocalModelDraft(_openCodeFiles.ConfigPath, model, RuntimeEndpointService.LocalOpenAiBaseUrl(launchSettings), RuntimeEndpointService.ModelApiKeyForClient(launchSettings), contextSize, outputLimit);
            _updatingOpenCodeModelEditor = true;
            _openCodeModelSnippetBox.Text = draft.Snippet;
            _updatingOpenCodeModelEditor = false;
            UpdateOpenCodeModelEditorState();
        }
        catch (Exception ex)
        {
            _updatingOpenCodeModelEditor = false;
            SetOpenCodeAddModelStatus(ex.Message);
            UpdateOpenCodeLocalModelActionButtons(valid: false, sameIdExists: false, sameConfig: false);
            UpdateOpenCodeExistingModelButtons(valid: false, matchesSaved: true);
        }
    }

    private async Task<AppSettings> OpenCodeLocalModelLaunchSettingsAsync(ModelRecord model)
    {
        var loaded = _sessions.SessionForModel(model.Id);
        if (loaded is { IsRunning: true })
            return await EnsureModelApiKeyAsync(loaded.LaunchSettings);

        var launchSettings = _settings;
        if (_stateStore is not null)
        {
            var profile = await EnsureModelLaunchProfileAsync(model);
            if (profile is not null)
                launchSettings = profile.ApplyTo(_settings);
        }

        return await EnsureModelApiKeyAsync(launchSettings);
    }

    private async Task<(int ContextSize, int OutputLimit)> OpenCodeModelLimitsAsync(ModelRecord model, AppSettings launchSettings, CancellationToken cancellationToken = default)
    {
        var contextSize = launchSettings.ContextSize;
        if (contextSize <= 0)
            contextSize = (await CachedModelCapabilitiesAsync(model, cancellationToken)).ContextLength;

        var outputLimit = contextSize > 0
            ? Math.Clamp(contextSize / 4, 4096, 32768)
            : 8192;
        return (contextSize, outputLimit);
    }

    private void UpdateOpenCodeModelEditorState()
    {
        var model = SelectedOpenCodeModel();
        var adding = model?.IsAddNew ?? true;
        if (_openCodeSaveModelButton is not null) _openCodeSaveModelButton.Visibility = adding ? Visibility.Collapsed : Visibility.Visible;
        if (_openCodeDeleteModelButton is not null) _openCodeDeleteModelButton.Visibility = adding ? Visibility.Collapsed : Visibility.Visible;

        if (adding)
        {
            UpdateOpenCodeExistingModelButtons(valid: false, matchesSaved: true);
            UpdateOpenCodeLocalModelAddState();
            return;
        }

        try
        {
            Require(_openCode);
            var matchesSaved = _openCode!.SnippetsEquivalent(_openCodeSelectedModelSnapshot, _openCodeModelSnippetBox?.Text ?? "");
            UpdateOpenCodeExistingModelButtons(valid: true, matchesSaved);
        }
        catch
        {
            UpdateOpenCodeExistingModelButtons(valid: false, matchesSaved: false);
        }
    }

    private void UpdateOpenCodeExistingModelButtons(bool valid, bool matchesSaved)
    {
        if (_openCodeSaveModelButton is not null)
        {
            _openCodeSaveModelButton.Content = matchesSaved ? "Saved" : "Update Config";
            _openCodeSaveModelButton.IsEnabled = valid && !matchesSaved;
        }
        if (_openCodeDeleteModelButton is not null)
        {
            var selected = SelectedOpenCodeModel();
            _openCodeDeleteModelButton.IsEnabled = selected is not null && !selected.IsAddNew;
        }
    }

    private void UpdateOpenCodeLocalModelAddState()
    {
        if (SelectedOpenCodeModel()?.IsAddNew != true) return;
        if (_openCodeLocalModelCombo?.SelectedItem is not ModelRecord model)
        {
            SetOpenCodeAddModelStatus("Choose a local model to add.");
            UpdateOpenCodeLocalModelActionButtons(valid: false, sameIdExists: false, sameConfig: false);
            return;
        }

        try
        {
            Require(_openCode);
            var analysis = _openCode!.AnalyzeLocalModelSnippet(_openCodeFiles.ConfigPath, model, _openCodeModelSnippetBox?.Text ?? "");
            if (!analysis.SnippetValid)
            {
                SetOpenCodeAddModelStatus(analysis.Error);
                UpdateOpenCodeLocalModelActionButtons(valid: false, analysis.SameIdExists, analysis.SameConfig);
                return;
            }

            if (analysis.SameIdExists && analysis.SameConfig)
            {
                SetOpenCodeAddModelStatus($"Already exists with the same config: {analysis.FullId}");
            }
            else if (analysis.SameIdExists)
            {
                SetOpenCodeAddModelStatus($"Same model id exists with different config: {analysis.FullId}");
            }
            else if (analysis.SimilarMatches.Count > 0)
            {
                SetOpenCodeAddModelStatus("Similar existing model: " + string.Join("; ", analysis.SimilarMatches.Take(3)));
            }
            else
            {
                SetOpenCodeAddModelStatus($"Ready to add: {analysis.FullId}");
            }

            UpdateOpenCodeLocalModelActionButtons(valid: true, analysis.SameIdExists, analysis.SameConfig);
        }
        catch (Exception ex)
        {
            SetOpenCodeAddModelStatus(ex.Message);
            UpdateOpenCodeLocalModelActionButtons(valid: false, sameIdExists: false, sameConfig: false);
        }
    }

    private void UpdateOpenCodeLocalModelActionButtons(bool valid, bool sameIdExists, bool sameConfig)
    {
        if (_openCodeAddLocalModelButton is not null)
        {
            _openCodeAddLocalModelButton.Visibility = sameIdExists ? Visibility.Collapsed : Visibility.Visible;
            _openCodeAddLocalModelButton.IsEnabled = valid && !sameIdExists;
        }
        if (_openCodeUpdateLocalModelButton is not null)
        {
            _openCodeUpdateLocalModelButton.Visibility = sameIdExists && !sameConfig ? Visibility.Visible : Visibility.Collapsed;
            _openCodeUpdateLocalModelButton.IsEnabled = valid && sameIdExists && !sameConfig;
        }
        if (_openCodeAddAsNewLocalModelButton is not null)
        {
            _openCodeAddAsNewLocalModelButton.Visibility = sameIdExists && !sameConfig ? Visibility.Visible : Visibility.Collapsed;
            _openCodeAddAsNewLocalModelButton.IsEnabled = valid && sameIdExists && !sameConfig;
        }
    }

    private void SetOpenCodeAddModelStatus(string text)
    {
        if (_openCodeAddModelStatusText is not null)
            _openCodeAddModelStatusText.Text = text;
    }

    private async Task SaveOpenCodeLocalModelSnippetAsync(bool addAsNew)
    {
        if (_openCodeLocalModelCombo?.SelectedItem is not ModelRecord model)
        {
            SetStatus("Choose a local model to add.");
            return;
        }

        await RunAsync(addAsNew ? "Adding local model as a new OpenCode model..." : "Saving local model to OpenCode...", async () =>
        {
            Require(_openCode);
            var launchSettings = await OpenCodeLocalModelLaunchSettingsAsync(model);
            var fullId = _openCode!.SaveLocalModelSnippet(_openCodeFiles.ConfigPath, model, RuntimeEndpointService.LocalOpenAiBaseUrl(launchSettings), RuntimeEndpointService.ModelApiKeyForClient(launchSettings), _openCodeModelSnippetBox?.Text ?? "", addAsNew);
            await RefreshOpenCodeAsync(preferredModelId: fullId);
            SetStatus(addAsNew ? $"Added OpenCode model {fullId}." : $"Saved OpenCode model {fullId}.");
        });
    }
}
