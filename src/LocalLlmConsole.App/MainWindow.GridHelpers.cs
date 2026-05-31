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
        => PageSectionFactory.GridFor(columns);

    private static void PolishGrid(DataGrid grid)
        => PageSectionFactory.PolishGrid(grid);

    private static DataTemplate RowDetailsTemplate(string binding)
        => PageSectionFactory.RowDetailsTemplate(binding);

    private static Border GridFrame(DataGrid grid)
        => PageSectionFactory.GridFrame(grid);

    private static Grid GridSection(string title, DataGrid grid, string description = "")
        => PageSectionFactory.GridSection(title, grid, description);

    private static Grid FramedSection(string title, UIElement child)
        => PageSectionFactory.FramedSection(title, child);

    private static GridSplitter HorizontalGridSplitter(int row)
        => PageSectionFactory.HorizontalGridSplitter(row);

    private static GridSplitter VerticalGridSplitter(int column)
        => PageSectionFactory.VerticalGridSplitter(column);

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
        => PageSectionFactory.ConfigureGridColumns(grid, columns);

    private static void ApplyGridTextMargin(DataGrid grid, Thickness margin)
        => PageSectionFactory.ApplyGridTextMargin(grid, margin);

    private static void ApplyRuntimeJobsRowStyle(DataGrid grid)
        => PageSectionFactory.ApplyRuntimeJobsRowStyle(grid);

    private static void AddButtonColumn(DataGrid grid, string header, string contentBinding, string enabledBinding, RoutedEventHandler click, double weight, string tooltipBinding = "")
        => PageSectionFactory.AddButtonColumn(grid, header, contentBinding, enabledBinding, click, weight, tooltipBinding, ButtonToolTip);
}
