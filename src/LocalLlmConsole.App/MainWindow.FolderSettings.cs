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
    private async Task ChooseModelsFolderAsync(bool scanAfter)
    {
        var folder = PickFolder(_settings.ModelsRoot);
        if (folder is null) return;
        await RunAsync("Changing models folder...", async () =>
        {
            _settings = _settings with { ModelsRoot = Path.GetFullPath(folder) };
            await PersistSettingsAsync();
            if (scanAfter) await _catalog!.ScanAsync(_settings.ModelsRoot);
            await RefreshAllAsync();
            if (_viewModel.CurrentPage == "Models") ShowModels();
            if (_viewModel.CurrentPage == "Settings") ShowSettings();
            SetStatus($"Models folder set to {_settings.ModelsRoot}");
        });
    }

    private async Task ChooseRuntimeFolderAsync(bool scanAfter)
    {
        var folder = PickFolder(_settings.RuntimeRoot);
        if (folder is null) return;
        await RunAsync("Changing runtimes folder...", async () =>
        {
            _settings = _settings with { RuntimeRoot = Path.GetFullPath(folder) };
            await PersistSettingsAsync();
            if (scanAfter)
            {
                await _runtimes!.ScanAsync(_settings.RuntimeRoot);
                _autoScannedRuntimeRoots.Add(Path.GetFullPath(_settings.RuntimeRoot));
            }
            await RefreshAllAsync();
            if (_viewModel.CurrentPage == "Runtimes") ShowRuntimes();
            if (_viewModel.CurrentPage == "Settings") ShowSettings();
            SetStatus($"Runtimes folder set to {_settings.RuntimeRoot}");
        });
    }

    private async Task PersistSettingsAsync()
    {
        Require(_stateStore);
        Directory.CreateDirectory(_workspaceRoot);
        Directory.CreateDirectory(_settings.ModelsRoot);
        Directory.CreateDirectory(_settings.RuntimeRoot);
        Directory.CreateDirectory(_settings.CacheRoot);
        await _stateStore!.SaveAppSettingsAsync(_settings with { WorkspaceRoot = _workspaceRoot });
    }
}
