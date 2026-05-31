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
    private void ShowSettings()
    {
        SetPage("Settings", "Application preferences.");
        _viewModel.Settings.ReplaceRows(_coreServices.App.SettingsPageDefinitions.BuildRows(_settings));

        var page = SettingsPageFactory.Create(new SettingsPageRequest(
            _viewModel.Settings.Rows,
            _settings.ThemeMode,
            _pageControllers.Settings.Build(),
            ButtonToolTip));
        _settingsPage.Apply(page);
        PageHost.Content = page.Root;
    }

    private void PreviewSettingsTheme()
    {
        var mode = AppPreferenceService.ThemeMode(_settingsPage.SelectedThemeValue);
        _settings = _settings with { ThemeMode = mode };
        ApplyTheme(mode);
        SetStatus("Theme preview applied. Save settings to keep it.");
    }

    private async Task RunSettingsRowActionAsync(EditableSettingRow? row)
        => await _coreServices.App.SettingsRowActions.RunActionAsync(row, SettingsRowActionActions());

    private void ToggleSettingsSecret(EditableSettingRow? row)
        => _coreServices.App.SettingsRowActions.ToggleSecret(row, SetStatus);

    private void CopySettingsSecret(EditableSettingRow? row)
        => _coreServices.App.SettingsRowActions.CopySecret(row, SettingsSecretCopyActions());

    private SettingsRowActionApplicationActions SettingsRowActionActions()
        => new(ClearCacheAsync, PickFolder, SetStatus);

    private SettingsSecretCopyApplicationActions SettingsSecretCopyActions()
        => new(_coreServices.App.Clipboard.SetText, SetStatus);

    private static EditableSettingRow? SettingRowFromSender(object sender)
        => sender is WpfButton { Tag: EditableSettingRow row } ? row : null;

    private async Task ClearCacheAsync()
    {
        var cacheClear = AppServices.CacheClearWorkflow;
        Require(cacheClear);
        await _coreServices.App.CacheClearApplication.ClearAsync(_settings, CacheClearActions(cacheClear!));
    }

    private CacheClearApplicationActions CacheClearActions(CacheClearWorkflowService cacheClear)
        => new(
            async (settings, hasActiveDownloads, token) => await cacheClear.PlanAsync(settings, hasActiveDownloads, token),
            async (settings, token) => await cacheClear.ClearAsync(settings, token),
            () => AppServices.HuggingFace?.ActiveDownloadCount > 0,
            () => _viewModel.CurrentPage == "Settings",
            ShowSettings,
            NotifyCacheClearPrompt,
            ConfirmCacheClearPrompt,
            RunAsync,
            SetStatus);

    private bool ConfirmCacheClearPrompt(CacheClearPrompt prompt)
        => _coreServices.App.Dialogs.Confirm(this, prompt.Message, prompt.Title, CacheClearMessageBoxImage(prompt.Kind));

    private void NotifyCacheClearPrompt(CacheClearPrompt prompt)
        => _coreServices.App.Dialogs.Notify(this, prompt.Message, prompt.Title, CacheClearMessageBoxImage(prompt.Kind));

    private static MessageBoxImage CacheClearMessageBoxImage(CacheClearPromptKind kind)
        => kind == CacheClearPromptKind.Warning
            ? MessageBoxImage.Warning
            : MessageBoxImage.Information;
}
