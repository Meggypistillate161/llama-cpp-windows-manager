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
    private void ShowLogs()
    {
        SetPage("Logs", "App and model log files.");
        var root = new Grid { Margin = new Thickness(16) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(230) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
        root.RowDefinitions.Add(new RowDefinition());

        var toolbar = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition());
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var leftActions = Bar();
        leftActions.Margin = new Thickness(0);
        leftActions.Children.Add(Button("Refresh Logs", async (_, _) => await RefreshLogsAsync()));
        leftActions.Children.Add(Button("Open Selected", (_, _) => OpenSelectedLogFile()));
        leftActions.Children.Add(Button("Open Logs Folder", (_, _) => OpenFolder(Path.Combine(_workspaceRoot, "logs"))));
        toolbar.Children.Add(leftActions);
        var rightActions = Bar();
        rightActions.Margin = new Thickness(0);
        rightActions.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
        rightActions.Children.Add(Button("Delete Selected", async (_, _) => await DeleteSelectedLogAsync()));
        rightActions.Children.Add(Button("Delete All Logs", async (_, _) => await DeleteAllLogsAsync()));
        Grid.SetColumn(rightActions, 2);
        toolbar.Children.Add(rightActions);
        Grid.SetRow(toolbar, 0);
        root.Children.Add(toolbar);

        _logsGrid = GridFor(("Type", "C1", .9), ("File", "C2", 2.1), ("Related", "C3", 2.5), ("Updated", "C4", 1.1), ("Size", "C5", .7));
        _logsGrid.SelectionMode = DataGridSelectionMode.Extended;
        _logsGrid.SelectionUnit = DataGridSelectionUnit.FullRow;
        AddButtonColumn(_logsGrid, "Open", "C6", "B1", OpenLogRow_Click, .55, tooltipBinding: "T1");
        AddButtonColumn(_logsGrid, "Delete", "C7", "B2", DeleteLogRow_Click, .65, tooltipBinding: "T2");
        _logsGrid.ItemsSource = _viewModel.Logs.Rows;
        _logsGrid.SelectionChanged += (_, _) => LoadSelectedLog();
        var listFrame = GridFrame(_logsGrid);
        Grid.SetRow(listFrame, 1);
        root.Children.Add(listFrame);

        root.Children.Add(HorizontalGridSplitter(2));

        _logsBox = new WpfTextBox
        {
            IsReadOnly = true,
            TextWrapping = TextWrapping.NoWrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        var viewer = new Border
        {
            Background = (System.Windows.Media.Brush)WpfApplication.Current.Resources["InputBack"],
            BorderBrush = (System.Windows.Media.Brush)WpfApplication.Current.Resources["PanelBorder"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(0, 6, 0, 6),
            Child = _logsBox
        };
        Grid.SetRow(viewer, 3);
        root.Children.Add(viewer);

        PageHost.Content = root;
        RunBackground(RefreshLogsAsync, "Logs refresh failed");
    }

    private async Task RefreshLogsAsync()
    {
        var selectedPaths = SelectedLogPaths().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var logRoot = Path.Combine(_workspaceRoot, "logs");
        Directory.CreateDirectory(logRoot);

        var jobs = _stateStore is null
            ? new Dictionary<string, JobRecord>(StringComparer.OrdinalIgnoreCase)
            : (await _stateStore.ListJobsAsync()).ToDictionary(job => LogFileService.NormalizePath(job.LogPath), StringComparer.OrdinalIgnoreCase);
        var activeModel = _llama.IsRunning ? await ActiveModelDisplayNameAsync(_llama.ActiveModelId) : "";
        var activeLogPath = LogFileService.NormalizePath(_llama.LogPath);

        var files = await Task.Run(() => Directory.EnumerateFiles(logRoot, "*.log", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .ToArray());
        _viewModel.Logs.ReplaceLogs(files, jobs, activeLogPath, activeModel);

        if (_logsGrid is not null)
        {
            _logsGrid.SelectedItems.Clear();
            foreach (var row in _viewModel.Logs.Rows.Where(row => selectedPaths.Contains(LogPathFromRow(row))))
                _logsGrid.SelectedItems.Add(row);
            if (_logsGrid.SelectedItems.Count == 0)
                _logsGrid.SelectedItem = _viewModel.Logs.Rows.FirstOrDefault();
            _logsGrid.Items.Refresh();
        }

        LoadSelectedLog();
    }

    private void LoadSelectedLog()
        => RunBackground(LoadSelectedLogAsync, "Log preview failed");

    private async Task LoadSelectedLogAsync()
    {
        if (_logsBox is null) return;
        var row = _logsGrid?.SelectedItem as UiRow;
        var path = LogPathFromRow(row);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            _logsBox.Text = _viewModel.Logs.Rows.Count == 0 ? "No app or model logs yet." : "Select a log file to view it.";
            return;
        }

        try
        {
            var heading = $"{row?.C1 ?? "Log"} | {Path.GetFileName(path)}{Environment.NewLine}{path}{Environment.NewLine}{row?.C3}{Environment.NewLine}Updated {row?.C4} | {row?.C5}";
            var tail = await Task.Run(() => LogFileService.Tail(path, 80000));
            tail = LogFileService.RedactSensitiveText(tail, _settings.ModelApiKey);
            if (!string.Equals(path, SelectedLogPath(), StringComparison.OrdinalIgnoreCase)) return;
            _logsBox.Text = $"{heading}{Environment.NewLine}{Environment.NewLine}{tail}";
            _logsBox.CaretIndex = _logsBox.Text.Length;
            _logsBox.ScrollToEnd();
        }
        catch (Exception ex)
        {
            _logsBox.Text = $"Could not read log file.{Environment.NewLine}{Environment.NewLine}{ex.Message}";
        }
    }

    private void OpenSelectedLogFile()
    {
        var path = SelectedLogPath();
        if (!TryValidateLogFileForOpen(path, out var error))
        {
            SetStatus(error);
            return;
        }

        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private void OpenLogRow_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not UiRow row) return;
        var path = LogPathFromRow(row);
        if (!TryValidateLogFileForOpen(path, out var error))
        {
            SetStatus(error);
            return;
        }

        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private async void DeleteLogRow_Click(object sender, RoutedEventArgs e)
    {
        await RunEventAsync(async () =>
        {
            if ((sender as FrameworkElement)?.Tag is UiRow row)
                await DeleteLogPathAsync(LogPathFromRow(row));
        });
    }

    private async Task DeleteSelectedLogAsync()
    {
        var paths = SelectedLogPaths().Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (paths.Length == 0)
        {
            SetStatus("Select one or more log files first.");
            return;
        }

        if (paths.Length == 1)
        {
            await DeleteLogPathAsync(paths[0]);
            return;
        }

        var deletionPlan = LogFileService.BuildDeletionPlan(_workspaceRoot, paths, _llama.LogPath);

        if (deletionPlan.DeletablePaths.Count == 0)
        {
            SetStatus("No selected logs can be deleted. Stop the running model before deleting its active runtime log.");
            return;
        }

        if (ThemedMessageBox.Show(this, $"Delete {deletionPlan.DeletablePaths.Count} selected log files?", "Delete selected logs", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        await RunAsync("Deleting selected logs...", async () =>
        {
            var deleted = await Task.Run(() => LogFileService.DeleteLogs(deletionPlan.DeletablePaths));
            if (_logsBox is not null) _logsBox.Text = "";
            await RefreshLogsAsync();
            SetStatus(LogFileService.FormatDeletionStatus(deleted, deletionPlan.SkippedCount, "selected log file"));
        });
    }

    private async Task DeleteLogPathAsync(string path)
    {
        if (!TryValidateLogFileForOpen(path, out var error))
        {
            SetStatus(error);
            return;
        }

        if (IsActiveRuntimeLog(path))
        {
            SetStatus("Stop the running model before deleting its active runtime log.");
            return;
        }

        if (ThemedMessageBox.Show(this, $"Delete this log file?\n\n{Path.GetFileName(path)}", "Delete log", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        await RunAsync("Deleting log...", async () =>
        {
            await Task.Run(() => LogFileService.DeleteLogs([path]));
            if (_logsBox is not null) _logsBox.Text = "";
            await RefreshLogsAsync();
            SetStatus($"Deleted log {Path.GetFileName(path)}.");
        });
    }

    private async Task DeleteAllLogsAsync()
    {
        var logRoot = Path.Combine(_workspaceRoot, "logs");
        Directory.CreateDirectory(logRoot);
        var candidates = await Task.Run(() => Directory.EnumerateFiles(logRoot, "*.log", SearchOption.TopDirectoryOnly).ToArray());
        if (candidates.Length == 0)
        {
            SetStatus("No logs to delete.");
            return;
        }

        if (ThemedMessageBox.Show(this, $"Delete all log files in:\n\n{logRoot}", "Delete all logs", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        await RunAsync("Deleting logs...", async () =>
        {
            var deletionPlan = LogFileService.BuildDeletionPlan(_workspaceRoot, candidates, _llama.LogPath);
            var deleted = await Task.Run(() => LogFileService.DeleteLogs(deletionPlan.DeletablePaths));
            if (_logsBox is not null) _logsBox.Text = "";
            await RefreshLogsAsync();
            SetStatus(LogFileService.FormatDeletionStatus(deleted, deletionPlan.SkippedCount, "log file"));
        });
    }

    private bool IsActiveRuntimeLog(string path)
        => _llama.IsRunning
           && !string.IsNullOrWhiteSpace(_llama.LogPath)
           && string.Equals(LogFileService.NormalizePath(path), LogFileService.NormalizePath(_llama.LogPath), StringComparison.OrdinalIgnoreCase);

    private bool TryValidateLogFileForOpen(string path, out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(path))
        {
            error = "Select a log file first.";
            return false;
        }

        return LogFileService.TryValidateWorkspaceLogFile(_workspaceRoot, path, out _, out error);
    }

    private string SelectedLogPath() => LogPathFromRow(_logsGrid?.SelectedItem as UiRow);

    private string[] SelectedLogPaths()
    {
        if (_logsGrid is null) return [];
        var paths = _logsGrid.SelectedItems
            .Cast<object>()
            .OfType<UiRow>()
            .Select(LogPathFromRow)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToArray();
        if (paths.Length > 0) return paths;
        var fallback = SelectedLogPath();
        return string.IsNullOrWhiteSpace(fallback) ? [] : [fallback];
    }

    private static string LogPathFromRow(UiRow? row)
        => row?.Data["Path"]?.ToString() ?? "";

    private async Task WriteAppLogAsync(Exception ex)
    {
        try
        {
            var logRoot = Path.Combine(_workspaceRoot, "logs");
            Directory.CreateDirectory(logRoot);
            var path = Path.Combine(logRoot, $"app-{DateTimeOffset.Now:yyyyMMdd}.log");
            var text = $"[{DateTimeOffset.Now:O}] ERROR {ex.GetType().Name}: {ex.Message}{Environment.NewLine}{ex}{Environment.NewLine}";
            text = LogFileService.RedactSensitiveText(text, _settings.ModelApiKey);
            await BoundedLogFile.AppendAsync(path, text, MaxLogBytes());
        }
        catch
        {
            // Logging must never create a second failure path.
        }
    }
}
