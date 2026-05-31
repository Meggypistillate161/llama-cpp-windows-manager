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

public static partial class LaunchSettingsPanelFactory
{
    private static Grid RuntimeAndPortRow(WpfComboBox runtimeCombo, WpfTextBox launchPortBox)
    {
        var runtimeGrid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        runtimeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(98) });
        runtimeGrid.ColumnDefinitions.Add(new ColumnDefinition());
        runtimeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        runtimeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(88) });
        runtimeGrid.Children.Add(new TextBlock
        {
            Text = "Runtime",
            Foreground = (WpfBrush)WpfApplication.Current.Resources["TextMuted"],
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 2)
        });
        Grid.SetColumn(runtimeCombo, 1);
        runtimeGrid.Children.Add(runtimeCombo);

        var portLabel = new TextBlock
        {
            Text = "Port",
            Foreground = (WpfBrush)WpfApplication.Current.Resources["TextMuted"],
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 7, 2),
            ToolTip = TooltipText("Fixed server port for this model. OpenCode uses this endpoint before the model is loaded.")
        };
        Grid.SetColumn(portLabel, 2);
        runtimeGrid.Children.Add(portLabel);
        Grid.SetColumn(launchPortBox, 3);
        runtimeGrid.Children.Add(launchPortBox);
        return runtimeGrid;
    }

    private static WpfComboBox RuntimeCombo(LaunchSettingsPanelRequest request)
    {
        var combo = new WpfComboBox
        {
            ItemsSource = request.RuntimeChoices,
            DisplayMemberPath = nameof(RuntimeChoice.Label),
            SelectedValuePath = nameof(RuntimeChoice.Id),
            MinHeight = 29,
            Margin = new Thickness(0, 0, 4, 2),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
            ToolTip = TooltipText("llama.cpp runtime used when starting or restarting the selected model.")
        };
        combo.SelectionChanged += (_, _) => request.RuntimeSelectionChanged();
        return combo;
    }

    private static WpfCheckBox AdvancedToggle(LaunchSettingsPanelRequest request)
    {
        var toggle = new WpfCheckBox
        {
            Content = "Advanced settings",
            IsChecked = request.ShowAdvancedLaunchSettings,
            Foreground = (WpfBrush)WpfApplication.Current.Resources["TextMain"],
            Margin = new Thickness(0, 0, 0, 8),
            ToolTip = TooltipText("Shows tuning controls for memory, RoPE, speculative/MTP decoding, and sampling.")
        };
        toggle.Checked += (_, _) => request.AdvancedSettingsChanged(true);
        toggle.Unchecked += (_, _) => request.AdvancedSettingsChanged(false);
        return toggle;
    }

    private static WrapPanel ActionButtons(LaunchSettingsPanelRequest request, out WpfButton saveForModelButton)
    {
        var actions = Bar();
        saveForModelButton = Button("Save For Model", request.SaveForModelAsync);
        actions.Children.Add(saveForModelButton);
        actions.Children.Add(Button("Save As Default", request.SaveDefaultsAsync));
        actions.Children.Add(Button("Reset Defaults", () =>
        {
            request.ResetDefaults();
            return Task.CompletedTask;
        }));
        return actions;
    }

    private static Grid SaveAsNewRow(LaunchSettingsPanelRequest request, out WpfTextBox nameBox, out WpfButton saveButton)
    {
        var saveAsNewGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        saveAsNewGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(98) });
        saveAsNewGrid.ColumnDefinitions.Add(new ColumnDefinition());
        saveAsNewGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        saveAsNewGrid.Children.Add(new TextBlock
        {
            Text = "Save as new",
            Foreground = (WpfBrush)WpfApplication.Current.Resources["TextMuted"],
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 2),
            ToolTip = TooltipText("Create a saved model variant from the selected model and the settings currently shown here.")
        });
        nameBox = new WpfTextBox
        {
            MinHeight = 29,
            Margin = new Thickness(0, 0, 6, 2),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
            ToolTip = TooltipText("Name for the saved model variant. Change the prefilled name before saving.")
        };
        nameBox.TextChanged += (_, _) => request.SaveAsNewNameChanged();
        Grid.SetColumn(nameBox, 1);
        saveAsNewGrid.Children.Add(nameBox);
        saveButton = Button("Save As New", request.SaveAsNewAsync);
        saveButton.ToolTip = TooltipText("Save the current launch settings as a separate loadable model variant on a new direct API port.");
        ToolTipService.SetShowOnDisabled(saveButton, true);
        Grid.SetColumn(saveButton, 2);
        saveAsNewGrid.Children.Add(saveButton);
        return saveAsNewGrid;
    }

    private static WpfTextBox LaunchTextBox(int value) => LaunchTextBox(value.ToString(CultureInfo.InvariantCulture));

    private static WpfTextBox LaunchTextBox(double value) => LaunchTextBox(value.ToString("0.###", CultureInfo.InvariantCulture));

    private static WpfTextBox LaunchTextBox(string value) => new()
    {
        Text = value,
        MinHeight = 29,
        MinWidth = 72,
        Margin = new Thickness(0, 0, 4, 2),
        HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch
    };

    private static WpfComboBox LaunchCombo(params string[] values) => LaunchCombo((IEnumerable<string>)values);

    private static WpfComboBox LaunchCombo(IEnumerable<string> values) => new()
    {
        ItemsSource = values.ToArray(),
        SelectedIndex = 0,
        MinHeight = 27,
        MinWidth = 76,
        Margin = new Thickness(0, 0, 6, 4),
        HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch
    };
}
