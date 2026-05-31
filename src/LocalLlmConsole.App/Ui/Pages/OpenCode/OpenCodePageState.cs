using System.Windows;
using System.Windows.Controls;
using WpfButton = System.Windows.Controls.Button;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace LocalLlmConsole;

public sealed class OpenCodePageState
{
    public TextBlock? ConfigPathText { get; private set; }

    public TextBlock? AgentsPathText { get; private set; }

    public TextBlock? HealthText { get; private set; }

    public WpfComboBox? ModelCombo { get; private set; }

    public WpfComboBox? LocalModelCombo { get; private set; }

    public TextBlock? AddModelStatusText { get; private set; }

    public WpfComboBox? AgentCombo { get; private set; }

    public WpfComboBox? AgentKindCombo { get; private set; }

    public WpfTextBox? ModelSnippetBox { get; private set; }

    public WpfTextBox? AgentSnippetBox { get; private set; }

    public WpfTextBox? NewAgentNameBox { get; private set; }

    public FrameworkElement? AddModelPanel { get; private set; }

    public FrameworkElement? AddAgentPanel { get; private set; }

    public WpfButton? SaveModelButton { get; private set; }

    public WpfButton? DeleteModelButton { get; private set; }

    public WpfButton? AddLocalModelButton { get; private set; }

    public WpfButton? UpdateLocalModelButton { get; private set; }

    public WpfButton? AddAsNewLocalModelButton { get; private set; }

    public WpfButton? SaveAgentButton { get; private set; }

    public WpfButton? DeleteAgentButton { get; private set; }

    public WpfButton? CreateAgentButton { get; private set; }

    public OpenCodeModelEntry? SelectedModel => ModelCombo?.SelectedItem as OpenCodeModelEntry;

    public ModelRecord? SelectedLocalModel => LocalModelCombo?.SelectedItem as ModelRecord;

    public OpenCodeAgentEntry? SelectedAgent => AgentCombo?.SelectedItem as OpenCodeAgentEntry;

    public string ModelSnippet => ModelSnippetBox?.Text ?? "";

    public string AgentSnippet => AgentSnippetBox?.Text ?? "";

    public string NewAgentName => NewAgentNameBox?.Text ?? "";

    public void Apply(OpenCodePageControls controls)
    {
        ArgumentNullException.ThrowIfNull(controls);

        ConfigPathText = controls.ConfigPathText;
        AgentsPathText = controls.AgentsPathText;
        HealthText = controls.HealthText;
        ModelCombo = controls.ModelCombo;
        SaveModelButton = controls.SaveModelButton;
        DeleteModelButton = controls.DeleteModelButton;
        LocalModelCombo = controls.LocalModelCombo;
        AddModelStatusText = controls.AddModelStatusText;
        AddLocalModelButton = controls.AddLocalModelButton;
        UpdateLocalModelButton = controls.UpdateLocalModelButton;
        AddAsNewLocalModelButton = controls.AddAsNewLocalModelButton;
        AddModelPanel = controls.AddModelPanel;
        ModelSnippetBox = controls.ModelSnippetBox;
        AgentCombo = controls.AgentCombo;
        SaveAgentButton = controls.SaveAgentButton;
        DeleteAgentButton = controls.DeleteAgentButton;
        NewAgentNameBox = controls.NewAgentNameBox;
        AgentKindCombo = controls.AgentKindCombo;
        CreateAgentButton = controls.CreateAgentButton;
        AddAgentPanel = controls.AddAgentPanel;
        AgentSnippetBox = controls.AgentSnippetBox;
    }
}
