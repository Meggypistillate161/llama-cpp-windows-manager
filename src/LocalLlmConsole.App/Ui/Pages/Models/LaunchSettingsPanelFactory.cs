using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfApplication = System.Windows.Application;
using WpfBrush = System.Windows.Media.Brush;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace LocalLlmConsole;

public sealed record LaunchSettingsPanelRequest(
    AppSettings Settings,
    IEnumerable<RuntimeChoice> RuntimeChoices,
    bool ShowAdvancedLaunchSettings,
    Action RuntimeSelectionChanged,
    Action<bool> AdvancedSettingsChanged,
    Func<Task> SaveForModelAsync,
    Func<Task> SaveDefaultsAsync,
    Action ResetDefaults,
    Func<Task> SaveAsNewAsync,
    Func<Task> ChooseVisionProjectorAsync,
    Func<Task> ChooseMtpHeadAsync,
    Action SaveAsNewNameChanged);

public sealed class LaunchSettingsPanelControls
{
    public required UIElement Root { get; init; }
    public required WpfComboBox RuntimeCombo { get; init; }
    public required TextBlock ModelCapabilityText { get; init; }
    public required WpfCheckBox AdvancedLaunchSettingsToggle { get; init; }
    public required WpfButton SaveModelLaunchSettingsButton { get; init; }
    public required WpfTextBox SaveAsNewModelNameBox { get; init; }
    public required WpfButton SaveAsNewModelButton { get; init; }
    public required LaunchSettingsFormControls FormControls { get; init; }
    public required Dictionary<string, List<FrameworkElement>> LaunchSettingElements { get; init; }
    public required List<FrameworkElement> AdvancedLaunchSections { get; init; }
}

public static partial class LaunchSettingsPanelFactory
{
    public static LaunchSettingsPanelControls Create(LaunchSettingsPanelRequest request)
    {
        var launchSettingElements = new Dictionary<string, List<FrameworkElement>>(StringComparer.OrdinalIgnoreCase);
        var advancedLaunchSections = new List<FrameworkElement>();
        var panel = new StackPanel();

        var runtimeCombo = RuntimeCombo(request);
        var launchPortBox = LaunchTextBox(request.Settings.Port);
        launchPortBox.MinWidth = 78;
        launchPortBox.ToolTip = TooltipText("Fixed server port for this model. Use a unique port per model when serving multiple models.");
        panel.Children.Add(RuntimeAndPortRow(runtimeCombo, launchPortBox));

        var modelCapabilityText = Text("No model selected", 12, false, true);
        modelCapabilityText.TextWrapping = TextWrapping.NoWrap;
        modelCapabilityText.TextTrimming = TextTrimming.CharacterEllipsis;
        modelCapabilityText.Margin = new Thickness(0, 0, 0, 4);
        panel.Children.Add(modelCapabilityText);

        var advancedToggle = AdvancedToggle(request);
        panel.Children.Add(advancedToggle);

        var builder = new LaunchSettingsPanelBuilder(launchSettingElements, advancedLaunchSections);
        var formControls = AddLaunchSections(panel, builder, request, launchPortBox);

        panel.Children.Add(ActionButtons(request, out var saveForModelButton));
        panel.Children.Add(SaveAsNewRow(request, out var saveAsNewModelNameBox, out var saveAsNewModelButton));

        var root = new Border
        {
            Background = (WpfBrush)WpfApplication.Current.Resources["InputBack"],
            BorderBrush = (WpfBrush)WpfApplication.Current.Resources["PanelBorder"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(0),
            MinHeight = 220,
            Child = Scroll(panel, new Thickness(9, 8, 7, 8))
        };

        return new LaunchSettingsPanelControls
        {
            Root = root,
            RuntimeCombo = runtimeCombo,
            ModelCapabilityText = modelCapabilityText,
            AdvancedLaunchSettingsToggle = advancedToggle,
            SaveModelLaunchSettingsButton = saveForModelButton,
            SaveAsNewModelNameBox = saveAsNewModelNameBox,
            SaveAsNewModelButton = saveAsNewModelButton,
            FormControls = formControls,
            LaunchSettingElements = launchSettingElements,
            AdvancedLaunchSections = advancedLaunchSections
        };
    }
}
