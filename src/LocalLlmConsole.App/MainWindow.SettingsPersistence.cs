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
    private async Task SaveSettingsAsync()
    {
        string V(string key, string fallback) => _viewModel.Settings.Rows.FirstOrDefault(row => row.Key == key)?.Value ?? fallback;
        var accessMode = AppPreferenceService.ModelAccessMode(V("modelAccessMode", _settings.ModelAccessMode));
        var apiKey = (V("modelApiKey", _settings.ModelApiKey) ?? "").Trim();
        var generatedApiKey = false;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            apiKey = ApiSecurity.GenerateHexToken(32);
            generatedApiKey = true;
        }
        if (!ApiSecurity.IsStrongBearerSecret(apiKey))
        {
            SetStatus("Model API key must be at least 32 non-whitespace characters.");
            return;
        }
        var host = AppPreferenceService.RuntimeHostForAccessMode(accessMode);
        var port = AppPreferenceService.IntValue(V("port", _settings.Port.ToString()), _settings.Port);
        if (port is < 1 or > 65535)
        {
            SetStatus("Port must be between 1 and 65535.");
            return;
        }
        _settings = _settings with
        {
            WorkspaceRoot = _workspaceRoot,
            ThemeMode = AppPreferenceService.ThemeMode(ComboValue(_themeCombo)),
            MinimizeBehavior = AppPreferenceService.MinimizeBehavior(V("minimizeBehavior", _settings.MinimizeBehavior)),
            AutoUnloadIdleMinutes = AppPreferenceService.ClampedIntValue(V("autoUnloadIdleMinutes", _settings.AutoUnloadIdleMinutes.ToString()), _settings.AutoUnloadIdleMinutes, 0, 10080),
            DeleteRuntimeSourceAfterSuccessfulBuild = AppPreferenceService.YesNoValue(V("deleteRuntimeSourceAfterSuccessfulBuild", AppPreferenceService.YesNoLabel(_settings.DeleteRuntimeSourceAfterSuccessfulBuild)), _settings.DeleteRuntimeSourceAfterSuccessfulBuild),
            ModelAccessMode = accessMode,
            Host = host,
            ModelApiKey = apiKey,
            Port = port,
            MaxLogFileSizeMb = AppPreferenceService.ClampedIntValue(V("maxLogFileSizeMb", _settings.MaxLogFileSizeMb.ToString()), AppSettings.CreateDefault(_workspaceRoot).MaxLogFileSizeMb, 1, 4096)
        };
        ApplyTheme(_settings.ThemeMode);
        ApplyLaunchSettingsToControls();
        await PersistSettingsAsync();
        await SyncOpenCodeLocalProviderAsync(_settings);
        SetStatus(generatedApiKey ? "Settings saved. A model API key was generated." : "Settings saved.");
        if (_viewModel.CurrentPage == "Settings") ShowSettings();
    }

    private async Task<AppSettings> EnsureModelApiKeyAsync(AppSettings settings)
    {
        var apiKey = (settings.ModelApiKey ?? "").Trim();
        if (ApiSecurity.IsStrongBearerSecret(apiKey))
            return settings with { ModelApiKey = apiKey };

        apiKey = ApiSecurity.GenerateHexToken(32);
        _settings = _settings with { ModelApiKey = apiKey };
        await PersistSettingsAsync();
        var updated = settings with { ModelApiKey = apiKey };
        await SyncOpenCodeLocalProviderAsync(updated);
        return updated;
    }

    private async Task SyncOpenCodeLocalProviderAsync(AppSettings settings)
    {
        if (_openCode is null || string.IsNullOrWhiteSpace(RuntimeEndpointService.ModelApiKeyForClient(settings))) return;
        try
        {
            var fileSet = _openCode.LoadOrDetectFileSet();
            if (!_openCode.UpdateLocalProviderCredentials(fileSet.ConfigPath, RuntimeEndpointService.LocalOpenAiBaseUrl(settings), RuntimeEndpointService.ModelApiKeyForClient(settings)))
                return;

            _openCodeFiles = fileSet;
            if (_viewModel.CurrentPage == "OpenCode")
                await RefreshOpenCodeAsync();
        }
        catch (Exception ex)
        {
            await WriteAppLogAsync(ex);
        }
    }
}
