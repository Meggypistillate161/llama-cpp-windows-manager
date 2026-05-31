using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace LocalLlmConsole;

public sealed record HuggingFaceGridModeActions(
    RoutedEventHandler DownloadSearchRow,
    RoutedEventHandler OpenModelCardRow,
    RoutedEventHandler ResumeDownloadRow,
    RoutedEventHandler PauseDownloadRow,
    RoutedEventHandler StopDownloadRow,
    RoutedEventHandler DeleteDownloadRow);

public sealed record HuggingFaceGridModeRequest(
    DataGrid Grid,
    IEnumerable SearchRows,
    IEnumerable DownloadHistoryRows,
    HuggingFaceGridModeActions Actions,
    Action<DataGrid> ConfigureSearchColumnSizing,
    Action<DataGrid> ConfigureDownloadHistoryColumnSizing);

public static class HuggingFaceGridModeFactory
{
    public static void ConfigureSearch(HuggingFaceGridModeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Grid);
        ArgumentNullException.ThrowIfNull(request.Actions);

        PageSectionFactory.ConfigureGridColumns(
            request.Grid,
            ("Repo", "C1", 1.3),
            ("File", "C2", 2.3),
            ("Quant", "C3", .6),
            ("Size", "C4", .8),
            ("Downloads", "C5", .8),
            ("Signals", "C6", 1.4));
        PageSectionFactory.AddButtonColumn(request.Grid, "Actions", "C7", "B1", request.Actions.DownloadSearchRow, .8, tooltipBinding: "T1");
        PageSectionFactory.AddButtonColumn(request.Grid, "Card", "C8", "B2", request.Actions.OpenModelCardRow, .6, tooltipBinding: "T2");
        PageSectionFactory.ApplyGridTextMargin(request.Grid, new Thickness(6, 0, 6, 0));
        request.ConfigureSearchColumnSizing(request.Grid);
        request.Grid.SelectedItem = null;
        request.Grid.ItemsSource = request.SearchRows;
    }

    public static void ConfigureDownloadHistory(HuggingFaceGridModeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Grid);
        ArgumentNullException.ThrowIfNull(request.Actions);

        PageSectionFactory.ConfigureGridColumns(
            request.Grid,
            ("Status", "C1", .8),
            ("Model", "C2", 2.1),
            ("Progress", "C3", 1.1),
            ("Size", "C4", .8),
            ("Updated", "C5", 1),
            ("Destination", "C6", 2.4));
        PageSectionFactory.AddButtonColumn(request.Grid, "Start", "C7", "B1", request.Actions.ResumeDownloadRow, .7, tooltipBinding: "T1");
        PageSectionFactory.AddButtonColumn(request.Grid, "Pause", "C8", "B2", request.Actions.PauseDownloadRow, .7, tooltipBinding: "T2");
        PageSectionFactory.AddButtonColumn(request.Grid, "Stop", "C9", "B3", request.Actions.StopDownloadRow, .7, tooltipBinding: "T3");
        PageSectionFactory.AddButtonColumn(request.Grid, "Delete", "C10", "B4", request.Actions.DeleteDownloadRow, .7, tooltipBinding: "T4");
        PageSectionFactory.ApplyGridTextMargin(request.Grid, new Thickness(6, 0, 6, 0));
        request.ConfigureDownloadHistoryColumnSizing(request.Grid);
        request.Grid.SelectedItem = null;
        request.Grid.ItemsSource = request.DownloadHistoryRows;
    }
}
