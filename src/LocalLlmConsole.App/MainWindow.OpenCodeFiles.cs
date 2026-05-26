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
    private async Task DetectOpenCodeFilesAsync()
    {
        await RunAsync("Detecting OpenCode files...", async () =>
        {
            Require(_openCode);
            _openCodeFiles = _openCode!.DetectFileSet();
            _openCode.SaveFileSet(_openCodeFiles);
            await RefreshOpenCodeAsync();
            SetStatus("OpenCode files detected.");
        });
    }

    private async Task ChooseOpenCodeConfigFileAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Choose OpenCode config",
            Filter = "OpenCode config|opencode.json;opencode.jsonc|JSON files|*.json;*.jsonc|All files|*.*",
            CheckFileExists = false,
            AddExtension = true,
            DefaultExt = ".jsonc",
            FileName = Path.GetFileName(_openCodeFiles.ConfigPath)
        };
        var initialDirectory = Path.GetDirectoryName(_openCodeFiles.ConfigPath);
        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
            dialog.InitialDirectory = initialDirectory;
        if (dialog.ShowDialog(this) != true) return;

        await RunAsync("Setting OpenCode config...", async () =>
        {
            Require(_openCode);
            _openCodeFiles = _openCodeFiles with { ConfigPath = Path.GetFullPath(dialog.FileName) };
            _openCode!.SaveFileSet(_openCodeFiles);
            await RefreshOpenCodeAsync();
            SetStatus($"OpenCode config set to {_openCodeFiles.ConfigPath}");
        });
    }

    private async Task ChooseOpenCodeAgentsFolderAsync()
    {
        var folder = PickFolder(_openCodeFiles.AgentsDirectory);
        if (folder is null) return;
        await RunAsync("Setting OpenCode agents folder...", async () =>
        {
            Require(_openCode);
            _openCodeFiles = _openCodeFiles with { AgentsDirectory = Path.GetFullPath(folder) };
            _openCode!.SaveFileSet(_openCodeFiles);
            await RefreshOpenCodeAsync();
            SetStatus($"OpenCode agents folder set to {_openCodeFiles.AgentsDirectory}");
        });
    }

    private async Task CreateMissingOpenCodeFilesAsync()
    {
        await RunAsync("Creating OpenCode files...", async () =>
        {
            Require(_openCode);
            _openCode!.EnsureFiles(_openCodeFiles);
            _openCode.SaveFileSet(_openCodeFiles);
            await RefreshOpenCodeAsync();
            SetStatus("OpenCode config and agents folder are ready.");
        });
    }

    private void OpenOpenCodeConfigFolder()
    {
        var folder = Path.GetDirectoryName(_openCodeFiles.ConfigPath);
        if (!string.IsNullOrWhiteSpace(folder)) OpenFolder(folder);
    }

    private void UpdateOpenCodePathText()
    {
        if (_openCodeConfigPathText is not null) _openCodeConfigPathText.Text = _openCodeFiles.ConfigPath;
        if (_openCodeAgentsPathText is not null) _openCodeAgentsPathText.Text = _openCodeFiles.AgentsDirectory;
    }
}
