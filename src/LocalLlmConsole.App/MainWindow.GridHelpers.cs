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
    private static DataGrid GridFor(params (string Header, string Binding, double Weight)[] columns)
    {
        var grid = new DataGrid();
        PolishGrid(grid);
        ConfigureGridColumns(grid, columns);
        return grid;
    }

    private static void PolishGrid(DataGrid grid)
    {
        grid.BorderThickness = new Thickness(0);
        grid.Margin = new Thickness(0);
        ScrollViewer.SetHorizontalScrollBarVisibility(grid, ScrollBarVisibility.Auto);
        ScrollViewer.SetVerticalScrollBarVisibility(grid, ScrollBarVisibility.Auto);
    }

    private static DataTemplate RowDetailsTemplate(string binding)
    {
        var factory = new FrameworkElementFactory(typeof(TextBlock));
        factory.SetBinding(TextBlock.TextProperty, new WpfBinding(binding));
        factory.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
        factory.SetValue(TextBlock.ForegroundProperty, (System.Windows.Media.Brush)WpfApplication.Current.Resources["TextMuted"]);
        factory.SetValue(TextBlock.MarginProperty, new Thickness(14, 2, 14, 8));
        return new DataTemplate { VisualTree = factory };
    }

    private static Border GridFrame(DataGrid grid) => new()
    {
        Background = (System.Windows.Media.Brush)WpfApplication.Current.Resources["InputBack"],
        BorderBrush = (System.Windows.Media.Brush)WpfApplication.Current.Resources["PanelBorder"],
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(6),
        Margin = new Thickness(0, 6, 0, 6),
        Child = grid
    };

    private static Grid GridSection(string title, DataGrid grid, string description = "")
    {
        var section = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        section.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        section.RowDefinitions.Add(new RowDefinition());
        var header = new StackPanel { Margin = new Thickness(2, 0, 0, 4) };
        header.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = (System.Windows.Media.Brush)WpfApplication.Current.Resources["TextSoft"],
            Margin = new Thickness(0, 0, 0, string.IsNullOrWhiteSpace(description) ? 0 : 2)
        });
        if (!string.IsNullOrWhiteSpace(description))
        {
            header.Children.Add(new TextBlock
            {
                Text = description,
                FontSize = 12,
                Foreground = (System.Windows.Media.Brush)WpfApplication.Current.Resources["TextMuted"],
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 0)
            });
        }

        section.Children.Add(header);
        var frame = GridFrame(grid);
        Grid.SetRow(frame, 1);
        section.Children.Add(frame);
        return section;
    }

    private static GridSplitter HorizontalGridSplitter(int row)
    {
        var splitter = new GridSplitter
        {
            Height = 8,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ResizeDirection = GridResizeDirection.Rows,
            ResizeBehavior = GridResizeBehavior.PreviousAndNext,
            ShowsPreview = false,
            Background = (System.Windows.Media.Brush)WpfApplication.Current.Resources["PanelBorder"],
            Margin = new Thickness(0, 2, 0, 2)
        };
        Grid.SetRow(splitter, row);
        return splitter;
    }

    private static GridSplitter VerticalGridSplitter(int column)
    {
        var splitter = new GridSplitter
        {
            Width = 8,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ResizeDirection = GridResizeDirection.Columns,
            ResizeBehavior = GridResizeBehavior.PreviousAndNext,
            ShowsPreview = false,
            Background = (System.Windows.Media.Brush)WpfApplication.Current.Resources["PanelBorder"],
            Margin = new Thickness(2, 6, 2, 6)
        };
        Grid.SetColumn(splitter, column);
        return splitter;
    }

    private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child is not null)
        {
            if (child is T match) return match;
            child = System.Windows.Media.VisualTreeHelper.GetParent(child);
        }
        return null;
    }

    private static void ConfigureGridColumns(DataGrid grid, params (string Header, string Binding, double Weight)[] columns)
    {
        grid.Columns.Clear();
        var textStyle = (Style)WpfApplication.Current.Resources["GridCellText"];
        foreach (var col in columns)
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = col.Header,
                Binding = new WpfBinding(col.Binding),
                Width = new DataGridLength(col.Weight, DataGridLengthUnitType.Star),
                MinWidth = 56,
                CanUserResize = true,
                ElementStyle = textStyle
            });
    }

    private static void ApplyGridTextMargin(DataGrid grid, Thickness margin)
    {
        var textStyle = new Style(typeof(TextBlock), (Style)WpfApplication.Current.Resources["GridCellText"]);
        textStyle.Setters.Add(new Setter(FrameworkElement.MarginProperty, margin));
        foreach (var column in grid.Columns.OfType<DataGridTextColumn>())
            column.ElementStyle = textStyle;
    }

    private static void ApplyRuntimeJobsRowStyle(DataGrid grid)
    {
        var baseStyle = WpfApplication.Current.Resources[typeof(DataGridRow)] as Style;
        var style = baseStyle is null
            ? new Style(typeof(DataGridRow))
            : new Style(typeof(DataGridRow), baseStyle);

        var statusForeground = SolidBrush("#F2F5F8");
        var statuses = new (string Status, string Background)[]
        {
            ("Queued", "#173126"),
            ("Running", "#1E3F30"),
            ("Failed", "#472329"),
            ("Cancelled", "#3A2428"),
            ("Interrupted", "#3A2428")
        };
        foreach (var (status, background) in statuses)
            style.Triggers.Add(RuntimeJobStatusTrigger(status, background, statusForeground));
        grid.RowStyle = style;

        var textStyle = new Style(typeof(TextBlock), (Style)WpfApplication.Current.Resources["GridCellText"]);
        foreach (var (status, _) in statuses)
        {
            var trigger = new DataTrigger { Binding = new WpfBinding("C1"), Value = status };
            trigger.Setters.Add(new Setter(TextBlock.ForegroundProperty, statusForeground));
            textStyle.Triggers.Add(trigger);
        }
        foreach (var column in grid.Columns.OfType<DataGridTextColumn>())
            column.ElementStyle = textStyle;
    }

    private static DataTrigger RuntimeJobStatusTrigger(string status, string backgroundHex, System.Windows.Media.Brush foreground)
    {
        var brush = SolidBrush(backgroundHex);

        var trigger = new DataTrigger { Binding = new WpfBinding("C1"), Value = status };
        trigger.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, brush));
        trigger.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, foreground));
        return trigger;
    }

    private static SolidColorBrush SolidBrush(string color)
    {
        var brush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color)!);
        brush.Freeze();
        return brush;
    }

    private static void AddButtonColumn(DataGrid grid, string header, string contentBinding, string enabledBinding, RoutedEventHandler click, double weight, string tooltipBinding = "")
    {
        var factory = new FrameworkElementFactory(typeof(WpfButton));
        factory.SetBinding(ContentControl.ContentProperty, new WpfBinding(contentBinding));
        factory.SetBinding(UIElement.IsEnabledProperty, new WpfBinding(enabledBinding));
        factory.SetBinding(FrameworkElement.TagProperty, new WpfBinding("."));
        if (!string.IsNullOrWhiteSpace(tooltipBinding))
        {
            factory.SetBinding(FrameworkElement.ToolTipProperty, new WpfBinding(tooltipBinding));
        }
        else
        {
            var toolTip = ButtonToolTip(header);
            if (!string.IsNullOrWhiteSpace(toolTip))
                factory.SetValue(FrameworkElement.ToolTipProperty, toolTip);
        }
        factory.SetValue(ToolTipService.ShowOnDisabledProperty, true);
        factory.SetValue(FrameworkElement.MinHeightProperty, 22.0);
        factory.SetValue(System.Windows.Controls.Control.PaddingProperty, new Thickness(7, 1, 7, 2));
        factory.SetValue(FrameworkElement.MarginProperty, new Thickness(2, 1, 2, 1));
        factory.SetValue(FrameworkElement.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Stretch);
        var style = new Style(typeof(WpfButton), (Style)WpfApplication.Current.Resources[typeof(WpfButton)]);
        var emptyTrigger = new Trigger { Property = ContentControl.ContentProperty, Value = "" };
        emptyTrigger.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Collapsed));
        style.Triggers.Add(emptyTrigger);
        factory.SetValue(FrameworkElement.StyleProperty, style);
        factory.AddHandler(WpfButton.ClickEvent, click);

        grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = header,
            Width = new DataGridLength(weight, DataGridLengthUnitType.Star),
            MinWidth = 72,
            CanUserResize = true,
            CellTemplate = new DataTemplate { VisualTree = factory }
        });
    }
}
