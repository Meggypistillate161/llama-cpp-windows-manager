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
        var agent = SelectedOpenCodeAgent();
        if (agent is null || agent.IsAddNew) { SetStatus("Choose an OpenCode agent first."); return; }
        await RunAsync("Saving OpenCode agent...", async () =>
        {
            Require(_openCode);
            _openCode!.SaveAgentSnippet(_openCodeFiles.ConfigPath, agent, _openCodeAgentSnippetBox?.Text ?? "");
            await RefreshOpenCodeAsync(preferredAgentId: agent.Id);
            SetStatus($"Saved OpenCode agent {agent.Name}.");
        });
    }

    private async Task CreateOpenCodeAgentAsync()
    {
        var requestedName = _openCodeNewAgentNameBox?.Text.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(requestedName)) { SetStatus("Name the new agent first."); return; }
        var safeName = OpenCodeConfigService.SafeOpenCodeId(requestedName);
        var markdown = ComboValue(_openCodeAgentKindCombo).Contains("markdown", StringComparison.OrdinalIgnoreCase);
        var duplicate = _viewModel.OpenCode.AgentChoices.FirstOrDefault(agent => !agent.IsAddNew
            && agent.Kind == (markdown ? OpenCodeAgentKind.Markdown : OpenCodeAgentKind.Config)
            && string.Equals(agent.Name, safeName, StringComparison.OrdinalIgnoreCase));
        if (duplicate is not null
            && ThemedMessageBox.Show(this, $"Replace the existing OpenCode agent?\n\n{duplicate.Label}", "OpenCode agent", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        await RunAsync("Creating OpenCode agent...", async () =>
        {
            Require(_openCode);
            var selectedModel = SelectedOpenCodeModel();
            var modelFullId = selectedModel is null || selectedModel.IsAddNew ? "" : selectedModel.FullId;
            var created = _openCode!.CreateAgent(_openCodeFiles.ConfigPath, _openCodeFiles.AgentsDirectory, requestedName, markdown, modelFullId);
            await RefreshOpenCodeAsync(preferredAgentId: created.Id);
            SetStatus($"Created OpenCode agent {created.Name}.");
        });
    }

    private async Task DeleteOpenCodeAgentAsync()
    {
        var agent = SelectedOpenCodeAgent();
        if (agent is null || agent.IsAddNew) { SetStatus("Choose an OpenCode agent first."); return; }
        if (ThemedMessageBox.Show(this, $"Delete this OpenCode agent?\n\n{agent.Label}", "Delete OpenCode agent", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        await RunAsync("Deleting OpenCode agent...", async () =>
        {
            Require(_openCode);
            _openCode!.DeleteAgent(_openCodeFiles.ConfigPath, _openCodeFiles.AgentsDirectory, agent);
            await RefreshOpenCodeAsync();
            SetStatus($"Deleted OpenCode agent {agent.Name}.");
        });
    }
}
