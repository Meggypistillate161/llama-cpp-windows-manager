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
        await _coreServices.OpenCodeServices.OpenCodeFileSetApplication.DetectAsync(OpenCodeFileSetTransitionActions());
    }

    private async Task ChooseOpenCodeConfigFileAsync()
    {
        await _coreServices.OpenCodeServices.OpenCodeFileSetApplication.ChooseConfigPathAsync(
            _openCodeFileSet.Current,
            OpenCodeFileSetPickerActions());
    }

    private async Task ChooseOpenCodeAgentsFolderAsync()
    {
        await _coreServices.OpenCodeServices.OpenCodeFileSetApplication.ChooseAgentsDirectoryAsync(
            _openCodeFileSet.Current,
            OpenCodeFileSetPickerActions());
    }

    private async Task CreateMissingOpenCodeFilesAsync()
    {
        await _coreServices.OpenCodeServices.OpenCodeFileSetApplication.EnsureAsync(
            _openCodeFileSet.Current,
            OpenCodeFileSetTransitionActions());
    }

    private void OpenOpenCodeConfigFolder()
    {
        _coreServices.OpenCodeServices.OpenCodeFileSetApplication.OpenConfigFolder(
            _openCodeFileSet.Current,
            new OpenCodeConfigFolderOpenActions(OpenFolder));
    }

    private OpenCodePathApplicationActions OpenCodePathActions()
        => new(
            path =>
            {
                if (_openCodePage.ConfigPathText is not null)
                    _openCodePage.ConfigPathText.Text = path;
            },
            path =>
            {
                if (_openCodePage.AgentsPathText is not null)
                    _openCodePage.AgentsPathText.Text = path;
            });

    private OpenCodeFileSetApplicationActions OpenCodeFileSetActions()
        => new(
            files => _openCodeFileSet.Set(files),
            () => RefreshOpenCodeAsync(),
            SetStatus);

    private OpenCodeFileSetTransitionActions OpenCodeFileSetTransitionActions()
        => new(RunAsync, OpenCodeFileSetActions());

    private OpenCodeFileSetPickerActions OpenCodeFileSetPickerActions()
        => new(
            plan => _coreServices.App.FileSystemDialogs.PickOpenCodeConfigFile(plan, this),
            PickFolder,
            OpenCodeFileSetTransitionActions());
}
