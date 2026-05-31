using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfApplication = System.Windows.Application;
using WpfBrush = System.Windows.Media.Brush;
using WpfButton = System.Windows.Controls.Button;

namespace LocalLlmConsole;

public static partial class LaunchSettingsPanelFactory
{
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
            Background = (WpfBrush)WpfApplication.Current.Resources["AccentStrong"],
            CornerRadius = new CornerRadius(2),
            Margin = new Thickness(0, 1, 7, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        header.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = (WpfBrush)WpfApplication.Current.Resources["TextMain"],
            VerticalAlignment = VerticalAlignment.Center
        });

        var section = new StackPanel();
        section.Children.Add(header);
        section.Children.Add(new Border
        {
            Height = 1,
            Background = (WpfBrush)WpfApplication.Current.Resources["PanelBorder"],
            Margin = new Thickness(0, 0, 0, 7)
        });
        section.Children.Add(grid);

        return new Border
        {
            Background = (WpfBrush)WpfApplication.Current.Resources["PanelBack"],
            BorderBrush = (WpfBrush)WpfApplication.Current.Resources["PanelBorder"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 7, 8, 7),
            Margin = new Thickness(0, 0, 0, 8),
            Child = section
        };
    }

    private static WrapPanel Bar()
        => new() { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };

    private static WpfButton Button(string text, Func<Task> click)
    {
        var button = new WpfButton { Content = text };
        button.ToolTip = TooltipText(ButtonToolTip(text));
        ToolTipService.SetShowOnDisabled(button, true);
        button.Click += async (_, _) => await click();
        return button;
    }

    private static string ButtonToolTip(string text)
        => (text ?? "").Trim() switch
        {
            "Save For Model" => "Save these launch settings for the selected model.",
            "Save As Default" => "Save these launch settings as the default for new models.",
            "Reset Defaults" => "Restore launch settings to the app defaults.",
            "Save As New" => "Save the current launch settings as a separate loadable model variant on a new direct API port.",
            "Choose" => "Choose a GGUF file.",
            var label => string.IsNullOrWhiteSpace(label) ? "" : $"Run {label}."
        };

    private static ScrollViewer Scroll(UIElement child, Thickness? padding = null)
    {
        var viewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var content = new Border { Padding = padding ?? new Thickness(16), Child = child };
        content.SetBinding(FrameworkElement.WidthProperty, new System.Windows.Data.Binding(nameof(ScrollViewer.ViewportWidth)) { Source = viewer });
        viewer.Content = content;
        viewer.Loaded += (_, _) => viewer.Dispatcher.BeginInvoke(new Action(viewer.ScrollToTop), System.Windows.Threading.DispatcherPriority.ContextIdle);
        return viewer;
    }

    private static TextBlock Text(string text, int size = 13, bool bold = false, bool muted = false) => new()
    {
        Text = text,
        FontSize = size,
        FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
        Foreground = muted ? (WpfBrush)WpfApplication.Current.Resources["TextMuted"] : (WpfBrush)WpfApplication.Current.Resources["TextMain"],
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, size >= 18 ? 10 : 0, 0, size >= 18 ? 10 : 8)
    };

    private static string TooltipText(string text) => text;

    private sealed class LaunchSettingsPanelBuilder(
        Dictionary<string, List<FrameworkElement>> launchSettingElements,
        List<FrameworkElement> advancedLaunchSections)
    {
        public void AddLaunchSetting(Grid grid, string label, FrameworkElement control)
        {
            control.ToolTip = TooltipText(LaunchSettingMetadataService.Tooltip(label));
            var index = grid.Children.Count / 2;
            var row = index / 2;
            var rightSide = index % 2 == 1;
            if (!rightSide) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var labelText = new TextBlock
            {
                Text = label,
                Foreground = (WpfBrush)WpfApplication.Current.Resources["TextMuted"],
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
            launchSettingElements[label] = new List<FrameworkElement> { labelText, control };
        }

        public void AddAdvancedLaunchSetting(Grid grid, string label, FrameworkElement control)
        {
            AddLaunchSetting(grid, label, control);
            if (launchSettingElements.TryGetValue(label, out var elements))
                advancedLaunchSections.AddRange(elements);
        }

        public void AddAdvancedSection(FrameworkElement section)
            => advancedLaunchSections.Add(section);
    }
}
