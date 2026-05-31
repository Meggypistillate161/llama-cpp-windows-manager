using System.Collections;
using System.Windows;
using System.Windows.Controls;
using WpfApplication = System.Windows.Application;
using WpfBrush = System.Windows.Media.Brush;
using WpfButton = System.Windows.Controls.Button;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace LocalLlmConsole;

public sealed record LogsPageActions(
    RoutedEventHandler Refresh,
    RoutedEventHandler OpenSelected,
    RoutedEventHandler OpenLogsFolder,
    RoutedEventHandler DeleteSelected,
    RoutedEventHandler DeleteAll,
    RoutedEventHandler OpenRow,
    RoutedEventHandler DeleteRow,
    SelectionChangedEventHandler SelectionChanged);

public sealed record LogsPageRequest(
    IEnumerable Rows,
    LogsPageActions Actions,
    Func<string, string> ButtonToolTip);

public sealed record LogsPageControls(
    DataGrid LogsGrid,
    WpfTextBox LogsBox);

public sealed record LogsPageBuildResult(
    Grid Content,
    LogsPageControls Controls);

public static class LogsPageFactory
{
    public static LogsPageBuildResult Create(LogsPageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Rows);
        ArgumentNullException.ThrowIfNull(request.Actions);
        ArgumentNullException.ThrowIfNull(request.ButtonToolTip);

        var root = new Grid { Margin = new Thickness(16) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(230) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
        root.RowDefinitions.Add(new RowDefinition());

        var toolbar = Toolbar(request);
        Grid.SetRow(toolbar, 0);
        root.Children.Add(toolbar);

        var logsGrid = PageSectionFactory.GridFor(
            ("Type", "C1", .9),
            ("File", "C2", 2.1),
            ("Related", "C3", 2.5),
            ("Updated", "C4", 1.1),
            ("Size", "C5", .7));
        logsGrid.SelectionMode = DataGridSelectionMode.Extended;
        logsGrid.SelectionUnit = DataGridSelectionUnit.FullRow;
        PageSectionFactory.AddButtonColumn(logsGrid, "Open", "C6", "B1", request.Actions.OpenRow, .55, tooltipBinding: "T1");
        PageSectionFactory.AddButtonColumn(logsGrid, "Delete", "C7", "B2", request.Actions.DeleteRow, .65, tooltipBinding: "T2");
        logsGrid.ItemsSource = request.Rows;
        logsGrid.SelectionChanged += request.Actions.SelectionChanged;
        var listFrame = PageSectionFactory.GridFrame(logsGrid);
        Grid.SetRow(listFrame, 1);
        root.Children.Add(listFrame);

        root.Children.Add(PageSectionFactory.HorizontalGridSplitter(2));

        var logsBox = new WpfTextBox
        {
            IsReadOnly = true,
            TextWrapping = TextWrapping.NoWrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        var viewer = new Border
        {
            Background = (WpfBrush)WpfApplication.Current.Resources["InputBack"],
            BorderBrush = (WpfBrush)WpfApplication.Current.Resources["PanelBorder"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(0, 6, 0, 6),
            Child = logsBox
        };
        Grid.SetRow(viewer, 3);
        root.Children.Add(viewer);

        return new LogsPageBuildResult(root, new LogsPageControls(logsGrid, logsBox));
    }

    private static Grid Toolbar(LogsPageRequest request)
    {
        var toolbar = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition());
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var leftActions = Bar();
        leftActions.Children.Add(Button("Refresh Logs", request.Actions.Refresh, request.ButtonToolTip));
        leftActions.Children.Add(Button("Open Selected", request.Actions.OpenSelected, request.ButtonToolTip));
        leftActions.Children.Add(Button("Open Logs Folder", request.Actions.OpenLogsFolder, request.ButtonToolTip));
        toolbar.Children.Add(leftActions);

        var rightActions = Bar();
        rightActions.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
        rightActions.Children.Add(Button("Delete Selected", request.Actions.DeleteSelected, request.ButtonToolTip));
        rightActions.Children.Add(Button("Delete All Logs", request.Actions.DeleteAll, request.ButtonToolTip));
        Grid.SetColumn(rightActions, 2);
        toolbar.Children.Add(rightActions);
        return toolbar;
    }

    private static WrapPanel Bar() => new()
    {
        Orientation = System.Windows.Controls.Orientation.Horizontal,
        Margin = new Thickness(0)
    };

    private static WpfButton Button(string text, RoutedEventHandler click, Func<string, string> toolTip)
    {
        var button = new WpfButton
        {
            Content = text,
            ToolTip = toolTip(text)
        };
        ToolTipService.SetShowOnDisabled(button, true);
        button.Click += click;
        return button;
    }
}
