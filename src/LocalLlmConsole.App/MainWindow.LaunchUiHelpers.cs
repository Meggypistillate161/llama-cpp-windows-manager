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
    private static Grid LaunchSettingsGrid()
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(98) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(104) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        return grid;
    }

    private static Border LaunchSection(string title, Grid grid)
    {
        grid.Margin = new Thickness(0, 2, 0, 0);

        var header = new DockPanel
        {
            LastChildFill = true,
            Margin = new Thickness(0, 0, 0, 6)
        };
        header.Children.Add(new Border
        {
            Width = 3,
            Height = 16,
            Background = (System.Windows.Media.Brush)WpfApplication.Current.Resources["AccentStrong"],
            CornerRadius = new CornerRadius(2),
            Margin = new Thickness(0, 1, 7, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        header.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = (System.Windows.Media.Brush)WpfApplication.Current.Resources["TextMain"],
            VerticalAlignment = VerticalAlignment.Center
        });

        var section = new StackPanel();
        section.Children.Add(header);
        section.Children.Add(new Border
        {
            Height = 1,
            Background = (System.Windows.Media.Brush)WpfApplication.Current.Resources["PanelBorder"],
            Margin = new Thickness(0, 0, 0, 7)
        });
        section.Children.Add(grid);

        return new Border
        {
            Background = (System.Windows.Media.Brush)WpfApplication.Current.Resources["PanelBack"],
            BorderBrush = (System.Windows.Media.Brush)WpfApplication.Current.Resources["PanelBorder"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 7, 8, 7),
            Margin = new Thickness(0, 0, 0, 8),
            Child = section
        };
    }

    private void AddLaunchSetting(Grid grid, string label, FrameworkElement control)
    {
        control.ToolTip = TooltipText(LaunchSettingMetadataService.Tooltip(label));
        var index = grid.Children.Count / 2;
        var row = index / 2;
        var rightSide = index % 2 == 1;
        if (!rightSide) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var labelText = new TextBlock
        {
            Text = label,
            Foreground = (System.Windows.Media.Brush)WpfApplication.Current.Resources["TextMuted"],
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 7, 2)
        };
        Grid.SetRow(labelText, row);
        Grid.SetColumn(labelText, rightSide ? 3 : 0);
        grid.Children.Add(labelText);
        control.MinHeight = Math.Max(control.MinHeight, 29);
        control.MinWidth = Math.Max(control.MinWidth, 72);
        control.Margin = new Thickness(0, 0, 4, 2);
        control.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetRow(control, row);
        Grid.SetColumn(control, rightSide ? 4 : 1);
        grid.Children.Add(control);
        _launchSettingElements[label] = new List<FrameworkElement> { labelText, control };
    }

    private void AddAdvancedLaunchSetting(Grid grid, string label, FrameworkElement control)
    {
        AddLaunchSetting(grid, label, control);
        if (_launchSettingElements.TryGetValue(label, out var elements))
            _advancedLaunchSections.AddRange(elements);
    }

    private static string TooltipText(string text) => text;

    private static void SetText(WpfTextBox? box, int value) => SetText(box, value.ToString(CultureInfo.InvariantCulture));
    private static void SetText(WpfTextBox? box, double value) => SetText(box, value.ToString("0.###", CultureInfo.InvariantCulture));
    private static void SetText(WpfTextBox? box, string value)
    {
        if (box is not null) box.Text = value;
    }
    private static void SetCombo(WpfComboBox? combo, string value)
    {
        if (combo is null) return;
        var match = combo.Items.Cast<object>().Select(item => item.ToString() ?? "").FirstOrDefault(item => string.Equals(item, value, StringComparison.OrdinalIgnoreCase));
        combo.SelectedItem = string.IsNullOrWhiteSpace(match) ? combo.Items[0] : match;
    }
    private static string ComboValue(WpfComboBox? combo)
        => (combo?.SelectedItem?.ToString() ?? combo?.Text ?? "").Trim().ToLowerInvariant();
    private static int ReadContextSize(WpfTextBox? box)
        => LaunchSettingParser.ReadContextSize(box?.Text.Trim() ?? "");

    private void NormalizeContextSizeBox()
    {
        if (_contextSizeBox is null) return;
        var text = _contextSizeBox.Text.Trim();
        if (!LaunchSettingParser.TryNormalizeContextSize(text, out var value)) return;
        var normalized = value.ToString(CultureInfo.InvariantCulture);
        if (!string.Equals(text, normalized, StringComparison.Ordinal))
            _contextSizeBox.Text = normalized;
        UpdateContextSizeSuggestion();
    }
    private void UpdateContextSizeSuggestion()
    {
        if (_contextSizeBox is null) return;
        var text = _contextSizeBox.Text.Trim();
        _contextSizeBox.ToolTip = TooltipText(LaunchSettingMetadataService.ContextSizeTooltip(text));
    }
    private static int ReadInt(WpfTextBox? box, string label, int min, int? max = null)
        => LaunchSettingParser.ReadInt(box?.Text.Trim() ?? "", label, min, max);

    private static double ReadDouble(WpfTextBox? box, string label, double min, double? max = null)
        => LaunchSettingParser.ReadDouble(box?.Text.Trim() ?? "", label, min, max);
}
