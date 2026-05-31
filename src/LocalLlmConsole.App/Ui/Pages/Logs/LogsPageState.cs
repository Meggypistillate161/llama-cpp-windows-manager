using System.Windows.Controls;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace LocalLlmConsole;

public sealed class LogsPageState
{
    private DataGrid? LogsGrid { get; set; }

    private WpfTextBox? LogsBox { get; set; }

    public UiRow? SelectedLogRow => LogsGrid?.SelectedItem as UiRow;

    public string SelectedLogPath => LogPathFromRow(SelectedLogRow);

    public void Apply(LogsPageControls controls)
    {
        ArgumentNullException.ThrowIfNull(controls);

        LogsGrid = controls.LogsGrid;
        LogsBox = controls.LogsBox;
    }

    public void FocusLogsGrid()
        => LogsGrid?.Focus();

    public string[] SelectedLogPaths()
    {
        if (LogsGrid is null) return [];
        var paths = LogsGrid.SelectedItems
            .Cast<object>()
            .OfType<UiRow>()
            .Select(LogPathFromRow)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToArray();
        if (paths.Length > 0) return paths;
        var fallback = SelectedLogPath;
        return string.IsNullOrWhiteSpace(fallback) ? [] : [fallback];
    }

    public void RestoreSelection(ISet<string> selectedPaths, IReadOnlyList<UiRow> rows)
    {
        ArgumentNullException.ThrowIfNull(selectedPaths);
        ArgumentNullException.ThrowIfNull(rows);
        if (LogsGrid is null) return;

        LogsGrid.SelectedItems.Clear();
        foreach (var row in rows.Where(row => selectedPaths.Contains(LogPathFromRow(row))))
            LogsGrid.SelectedItems.Add(row);
        if (LogsGrid.SelectedItems.Count == 0)
            LogsGrid.SelectedItem = rows.FirstOrDefault();
        LogsGrid.Items.Refresh();
    }

    public bool HasPreviewBox => LogsBox is not null;

    public void ClearPreview()
    {
        if (LogsBox is not null) LogsBox.Text = "";
    }

    public void SetPreviewText(string text, bool scrollToEnd = false)
    {
        if (LogsBox is null) return;

        LogsBox.Text = text;
        if (!scrollToEnd) return;
        LogsBox.CaretIndex = LogsBox.Text.Length;
        LogsBox.ScrollToEnd();
    }

    private static string LogPathFromRow(UiRow? row)
        => row?.Data["Path"]?.ToString() ?? "";
}
