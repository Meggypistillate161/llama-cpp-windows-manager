using System.Windows;
using System.Windows.Controls;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace LocalLlmConsole;

public sealed class LaunchSettingsPanelState
{
    public WpfComboBox? RuntimeCombo { get; private set; }

    public TextBlock? ModelCapabilityText { get; private set; }

    public WpfCheckBox? AdvancedLaunchSettingsToggle { get; private set; }

    public LaunchSettingsFormControls FormControls { get; private set; } = new();

    public Dictionary<string, List<FrameworkElement>> LaunchSettingElements { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<FrameworkElement> AdvancedLaunchSections { get; } = [];

    private WpfButton? SaveModelLaunchSettingsButton { get; set; }

    private WpfTextBox? SaveAsNewModelNameBox { get; set; }

    private WpfButton? SaveAsNewModelButton { get; set; }

    public string SaveAsNewModelName => (SaveAsNewModelNameBox?.Text ?? "").Trim();

    public void Apply(LaunchSettingsPanelControls controls)
    {
        ArgumentNullException.ThrowIfNull(controls);

        LaunchSettingElements.Clear();
        foreach (var (key, value) in controls.LaunchSettingElements)
            LaunchSettingElements[key] = value;

        AdvancedLaunchSections.Clear();
        AdvancedLaunchSections.AddRange(controls.AdvancedLaunchSections);

        RuntimeCombo = controls.RuntimeCombo;
        ModelCapabilityText = controls.ModelCapabilityText;
        AdvancedLaunchSettingsToggle = controls.AdvancedLaunchSettingsToggle;
        SaveModelLaunchSettingsButton = controls.SaveModelLaunchSettingsButton;
        SaveAsNewModelNameBox = controls.SaveAsNewModelNameBox;
        SaveAsNewModelButton = controls.SaveAsNewModelButton;
        FormControls = controls.FormControls;
    }

    public void SetSaveForModelState(string content, bool enabled)
    {
        if (SaveModelLaunchSettingsButton is null) return;

        SaveModelLaunchSettingsButton.Content = content;
        SaveModelLaunchSettingsButton.IsEnabled = enabled;
    }

    public void SetSaveAsNewModelName(string name)
    {
        if (SaveAsNewModelNameBox is not null)
            SaveAsNewModelNameBox.Text = name ?? "";
    }

    public void SetSaveAsNewEnabled(bool enabled)
    {
        if (SaveAsNewModelButton is not null)
            SaveAsNewModelButton.IsEnabled = enabled;
    }

    public void ApplyControlState(LaunchSettingsControlStatePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        foreach (var section in AdvancedLaunchSections)
            section.Visibility = plan.ShowAdvancedSections ? Visibility.Visible : Visibility.Collapsed;

        ApplyLaunchSettingVisibility(plan.VisibleSettings);
        ApplyLaunchSettingEnabled(plan.EnabledSettings);

        if (FormControls.GpuLayersBox is not null)
            FormControls.GpuLayersBox.IsEnabled = plan.GpuLayersAvailable;
        if (FormControls.VisionCombo is not null)
            FormControls.VisionCombo.IsEnabled = plan.VisionLaunchSettingsAvailable;
        if (FormControls.VisionProjectorPathBox is not null)
            FormControls.VisionProjectorPathBox.IsEnabled = plan.VisionLaunchSettingsAvailable;
        if (FormControls.VisionProjectorButton is not null)
            FormControls.VisionProjectorButton.IsEnabled = plan.VisionLaunchSettingsAvailable;
        if (FormControls.VisionImageMinTokensBox is not null)
            FormControls.VisionImageMinTokensBox.IsEnabled = plan.VisionLaunchSettingsAvailable;
        if (FormControls.VisionImageMaxTokensBox is not null)
            FormControls.VisionImageMaxTokensBox.IsEnabled = plan.VisionLaunchSettingsAvailable;
        if (FormControls.MtpHeadPathBox is not null)
            FormControls.MtpHeadPathBox.IsEnabled = plan.MtpHeadSettingsAvailable;
        if (FormControls.MtpHeadButton is not null)
            FormControls.MtpHeadButton.IsEnabled = plan.MtpHeadSettingsAvailable;
    }

    private void ApplyLaunchSettingVisibility(IReadOnlyDictionary<string, bool> visibleSettings)
    {
        foreach (var (label, visible) in visibleSettings)
            SetLaunchSettingVisibility(label, visible);
    }

    private void SetLaunchSettingVisibility(string label, bool visible)
    {
        if (!LaunchSettingElements.TryGetValue(label, out var elements)) return;
        foreach (var element in elements)
            element.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyLaunchSettingEnabled(IReadOnlyDictionary<string, bool> enabledSettings)
    {
        foreach (var (label, enabled) in enabledSettings)
            SetLaunchSettingEnabled(label, enabled);
    }

    private void SetLaunchSettingEnabled(string label, bool enabled)
    {
        if (!LaunchSettingElements.TryGetValue(label, out var elements)) return;
        foreach (var element in elements)
            element.IsEnabled = enabled;
    }
}
