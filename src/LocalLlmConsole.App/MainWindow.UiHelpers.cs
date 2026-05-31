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
    private static StackPanel Stack() => new();
    private static DockPanel Dock() => new() { Margin = new Thickness(16) };
    private static WrapPanel Bar() => new() { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
    private static ScrollViewer Scroll(UIElement child, Thickness? padding = null)
    {
        var viewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var content = new Border { Padding = padding ?? new Thickness(16), Child = child };
        content.SetBinding(FrameworkElement.WidthProperty, new WpfBinding(nameof(ScrollViewer.ViewportWidth)) { Source = viewer });
        viewer.Content = content;
        viewer.Loaded += (_, _) => viewer.Dispatcher.BeginInvoke(new Action(viewer.ScrollToTop), System.Windows.Threading.DispatcherPriority.ContextIdle);
        return viewer;
    }
    private static TextBlock Text(string text, int size = 13, bool bold = false, bool muted = false) => new()
    {
        Text = text,
        FontSize = size,
        FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
        Foreground = muted ? (System.Windows.Media.Brush)WpfApplication.Current.Resources["TextMuted"] : (System.Windows.Media.Brush)WpfApplication.Current.Resources["TextMain"],
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, size >= 18 ? 10 : 0, 0, size >= 18 ? 10 : 8)
    };
    private static Grid AddMetric(Grid grid, string label, int row, int column)
        => MetricCardFactory.AddMetric(grid, label, row, column);

    private static Grid AddMetric(Grid grid, string label, int row, int column, out TextBlock lastKnown)
        => MetricCardFactory.AddMetric(grid, label, row, column, out lastKnown);

    private static Grid AddMetric(Grid grid, string label, int row, int column, bool includeProgress, out WpfProgressBar? progress)
        => MetricCardFactory.AddMetric(grid, label, row, column, includeProgress, out progress);

    private static Grid AddMetric(Grid grid, string label, int row, int column, bool includeProgress, out WpfProgressBar? progress, out TextBlock lastKnown)
        => MetricCardFactory.AddMetric(grid, label, row, column, includeProgress, out progress, out lastKnown);
    private static Grid FolderStrip(string label, string path, out TextBlock pathText, RoutedEventHandler choose, RoutedEventHandler open)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(new TextBlock { Text = label, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
        pathText = new TextBlock
        {
            Text = path,
            Foreground = (System.Windows.Media.Brush)WpfApplication.Current.Resources["TextMuted"],
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };
        Grid.SetColumn(pathText, 1);
        grid.Children.Add(pathText);
        var chooseButton = Button("Choose", choose);
        Grid.SetColumn(chooseButton, 2);
        grid.Children.Add(chooseButton);
        var openButton = Button("Open", open);
        Grid.SetColumn(openButton, 3);
        grid.Children.Add(openButton);
        return grid;
    }

    private static Grid FolderStripActionsFirst(string label, string path, out TextBlock pathText, params (string Text, RoutedEventHandler Click)[] actions)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        var column = 0;
        foreach (var _ in actions)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());

        foreach (var action in actions)
        {
            var button = Button(action.Text, action.Click);
            Grid.SetColumn(button, column++);
            grid.Children.Add(button);
        }

        var labelBlock = new TextBlock
        {
            Text = label,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 10, 6)
        };
        Grid.SetColumn(labelBlock, column++);
        grid.Children.Add(labelBlock);

        pathText = new TextBlock
        {
            Text = path,
            Foreground = (System.Windows.Media.Brush)WpfApplication.Current.Resources["TextMuted"],
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 6)
        };
        Grid.SetColumn(pathText, column);
        grid.Children.Add(pathText);
        return grid;
    }

    private static TextBlock AddFolderLine(System.Windows.Controls.Panel panel, string label, string path, RoutedEventHandler? choose, RoutedEventHandler open)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        if (choose is not null) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(new TextBlock { Text = label, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
        var pathText = new TextBlock
        {
            Text = path,
            Foreground = (System.Windows.Media.Brush)WpfApplication.Current.Resources["TextMuted"],
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };
        Grid.SetColumn(pathText, 1);
        grid.Children.Add(pathText);
        var openColumn = 2;
        if (choose is not null)
        {
            var chooseButton = Button("Choose", choose);
            Grid.SetColumn(chooseButton, 2);
            grid.Children.Add(chooseButton);
            openColumn = 3;
        }
        var openButton = Button("Open", open);
        Grid.SetColumn(openButton, openColumn);
        grid.Children.Add(openButton);
        panel.Children.Add(grid);
        return pathText;
    }
    private static void UpdateFolderText(TextBlock? textBlock, string value)
    {
        if (textBlock is not null) textBlock.Text = value;
    }
    private static WpfButton Button(string text, RoutedEventHandler click)
    {
        var button = new WpfButton { Content = text };
        SetButtonToolTip(button, ButtonToolTip(text));
        button.Click += click;
        return button;
    }

    private static void SetButtonToolTip(WpfButton? button, string toolTip)
    {
        if (button is null || string.IsNullOrWhiteSpace(toolTip)) return;
        button.ToolTip = TooltipText(toolTip);
        ToolTipService.SetShowOnDisabled(button, true);
    }

    private string? PickFolder(string initial)
        => _coreServices.App.FileSystemDialogs.PickFolder(initial);

    private void OpenFolder(string path)
        => _coreServices.App.ShellIntegration.OpenFolder(path);

    private void OpenUrl(string url)
        => _coreServices.App.ShellIntegration.OpenUrl(url);

    private static void SetMetricText(Grid? target, string value, bool emphasizeLoadedStatus = false)
        => MetricCardFactory.SetMetricText(target, value, emphasizeLoadedStatus);

    private static void SetLastKnownMetricText(TextBlock? target, DateTimeOffset capturedAt, DateTimeOffset now)
        => MetricCardFactory.SetLastKnownMetricText(target, capturedAt, now);

    private static void ClearLastKnownMetricText(TextBlock? target)
        => MetricCardFactory.ClearLastKnownMetricText(target);
}
