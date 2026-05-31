using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using WpfApplication = System.Windows.Application;
using WpfBinding = System.Windows.Data.Binding;
using WpfBrush = System.Windows.Media.Brush;
using WpfButton = System.Windows.Controls.Button;
using WpfComboBox = System.Windows.Controls.ComboBox;

namespace LocalLlmConsole;

public sealed record SettingsPageActions(
    RoutedEventHandler SaveSettings,
    SelectionChangedEventHandler ThemeChanged,
    RoutedEventHandler RevealSecret,
    RoutedEventHandler CopySecret,
    RoutedEventHandler RowAction);

public sealed record SettingsPageRequest(
    IEnumerable Rows,
    string ThemeMode,
    SettingsPageActions Actions,
    Func<string, string> ButtonToolTip);

public sealed record SettingsPageControls(
    DockPanel Root,
    WpfComboBox ThemeCombo,
    DataGrid SettingsGrid);

public static class SettingsPageFactory
{
    public static SettingsPageControls Create(SettingsPageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Rows);
        ArgumentNullException.ThrowIfNull(request.Actions);
        ArgumentNullException.ThrowIfNull(request.ButtonToolTip);

        var root = new DockPanel { Margin = new Thickness(16) };
        var toolbar = Toolbar(request, out var themeCombo);
        DockPanel.SetDock(toolbar, Dock.Top);
        root.Children.Add(toolbar);

        var grid = SettingsGrid(request);
        root.Children.Add(PageSectionFactory.GridFrame(grid));

        return new SettingsPageControls(root, themeCombo, grid);
    }

    private static Grid Toolbar(SettingsPageRequest request, out WpfComboBox themeCombo)
    {
        var toolbar = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition());
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var saveButton = Button("Save Settings", request.Actions.SaveSettings, request.ButtonToolTip);
        Grid.SetColumn(saveButton, 0);
        toolbar.Children.Add(saveButton);

        var themeBar = new WrapPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            Margin = new Thickness(0),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right
        };
        themeBar.Children.Add(new TextBlock
        {
            Text = "Theme",
            Foreground = (WpfBrush)WpfApplication.Current.Resources["TextMuted"],
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 6)
        });
        themeCombo = new WpfComboBox
        {
            ItemsSource = new[] { "system", "light", "dark" },
            SelectedItem = AppPreferenceService.ThemeMode(request.ThemeMode),
            Width = 110
        };
        themeCombo.SelectionChanged += request.Actions.ThemeChanged;
        themeBar.Children.Add(themeCombo);
        Grid.SetColumn(themeBar, 2);
        toolbar.Children.Add(themeBar);
        return toolbar;
    }

    private static DataGrid SettingsGrid(SettingsPageRequest request)
    {
        var grid = new DataGrid
        {
            IsReadOnly = false,
            ItemsSource = request.Rows,
            RowHeight = 38
        };
        PageSectionFactory.PolishGrid(grid);
        var textStyle = (Style)WpfApplication.Current.Resources["GridCellText"];
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Group",
            Binding = new WpfBinding(nameof(EditableSettingRow.Group)),
            IsReadOnly = true,
            ElementStyle = SettingsGridColumnFactory.CellTextStyle(textStyle),
            MinWidth = 80,
            Width = new DataGridLength(120),
            CanUserResize = true
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Setting",
            Binding = new WpfBinding(nameof(EditableSettingRow.Label)),
            IsReadOnly = true,
            ElementStyle = SettingsGridColumnFactory.CellTextStyle(textStyle),
            MinWidth = 110,
            Width = new DataGridLength(180),
            CanUserResize = true
        });
        grid.Columns.Add(SettingsGridColumnFactory.ValueColumn());
        grid.Columns.Add(SettingsGridColumnFactory.ActionsColumn(
            request.Actions.RevealSecret,
            request.Actions.CopySecret,
            request.Actions.RowAction));
        return grid;
    }

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
