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
    private async Task SaveOpenCodeAgentSnippetAsync()
    {
        await _coreServices.OpenCodeServices.OpenCodeAgentApplication.SaveSnippetAsync(
            new OpenCodeAgentSaveApplicationRequest(
                _openCodeFileSet.Current,
                SelectedOpenCodeAgent(),
                _openCodePage.AgentSnippet),
            OpenCodeAgentSaveActions());
    }

    private async Task CreateOpenCodeAgentAsync()
    {
        var markdown = ComboValue(_openCodePage.AgentKindCombo).Contains("markdown", StringComparison.OrdinalIgnoreCase);
        await _coreServices.OpenCodeServices.OpenCodeAgentApplication.CreateAsync(
            new OpenCodeAgentCreateApplicationRequest(
                _openCodeFileSet.Current,
                _openCodePage.NewAgentName,
                markdown,
                _viewModel.OpenCode.AgentChoices,
                SelectedOpenCodeModel()),
            OpenCodeAgentCreateActions());
    }

    private async Task DeleteOpenCodeAgentAsync()
    {
        await _coreServices.OpenCodeServices.OpenCodeAgentApplication.DeleteAsync(
            new OpenCodeAgentDeleteApplicationRequest(
                _openCodeFileSet.Current,
                SelectedOpenCodeAgent()),
            OpenCodeAgentDeleteActions());
    }

    private OpenCodeAgentCommandApplicationActions OpenCodeAgentCommandActions()
        => new(
            preferredAgentId => RefreshOpenCodeAsync(preferredAgentId: preferredAgentId),
            () => RefreshOpenCodeAsync(),
            SetStatus);

    private OpenCodeAgentSaveApplicationActions OpenCodeAgentSaveActions()
        => new(RunAsync, ConfirmOpenCodeCommand, OpenCodeAgentCommandActions());

    private OpenCodeAgentCreateApplicationActions OpenCodeAgentCreateActions()
        => new(RunAsync, ConfirmOpenCodeCommand, OpenCodeAgentCommandActions());

    private OpenCodeAgentDeleteApplicationActions OpenCodeAgentDeleteActions()
        => new(RunAsync, ConfirmOpenCodeCommand, OpenCodeAgentCommandActions());
}
