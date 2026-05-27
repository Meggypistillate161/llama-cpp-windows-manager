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
    private async Task ShowDownloadHistoryAsync(string selectedJobId = "")
    {
        if (_viewModel.CurrentPage != "Models" || _hfGrid is null)
        {
            ShowModels();
        }

        ConfigureDownloadHistoryGrid();
        await RefreshDownloadHistoryAsync();
        SelectDownloadHistoryJob(selectedJobId);
        StartDownloadHistoryRefreshTimer();
        SetStatus(string.IsNullOrWhiteSpace(selectedJobId) ? "Showing download history." : "Showing download history for the started model download.");
    }

    private async Task RefreshDownloadHistoryAsync()
    {
        if (_stateStore is null) return;
        var selectedId = SelectedDownloadJob()?.Id;
        _viewModel.HuggingFace.ReplaceDownloadHistory(await _stateStore.ListJobsAsync());

        if (_downloadHistoryGrid is not null && !string.IsNullOrWhiteSpace(selectedId))
            _downloadHistoryGrid.SelectedItem = _viewModel.HuggingFace.DownloadHistoryRows.FirstOrDefault(row => string.Equals(row.Data["Id"]?.ToString(), selectedId, StringComparison.OrdinalIgnoreCase));
    }

    private void SelectDownloadHistoryJob(string jobId)
    {
        if (_downloadHistoryGrid is null || string.IsNullOrWhiteSpace(jobId)) return;
        _downloadHistoryGrid.SelectedItem = _viewModel.HuggingFace.DownloadHistoryRows.FirstOrDefault(row =>
            string.Equals(row.Data["Id"]?.ToString(), jobId, StringComparison.OrdinalIgnoreCase));
        if (_downloadHistoryGrid.SelectedItem is not null)
            _downloadHistoryGrid.ScrollIntoView(_downloadHistoryGrid.SelectedItem);
    }

    private async Task ResumeSelectedDownloadAsync()
    {
        var job = SelectedDownloadJob();
        if (job is null) { SetStatus("Select a download history row first."); return; }
        await ResumeDownloadAsync(job);
    }

    private async Task ResumeDownloadAsync(JobRecord job)
    {
        if (job.Status is JobStatus.Running or JobStatus.Queued) { SetStatus("That download is already active."); return; }
        if (job.Status == JobStatus.Completed) { SetStatus("That download already completed."); return; }
        await RunAsync("Starting download...", async () =>
        {
            await _huggingFace!.ResumeDownloadAsync(job, _settings);
            await RefreshDownloadHistoryAsync();
            await RefreshJobsAsync();
            RunBackground(() => MonitorDownloadAsync(job.Id), "Download monitor failed");
            SetStatus($"Download started: {job.Id}");
        });
    }

    private async Task PauseSelectedDownloadAsync()
    {
        var job = SelectedDownloadJob();
        if (job is null) { SetStatus("Select a download history row first."); return; }
        await PauseDownloadAsync(job);
    }

    private async Task PauseDownloadAsync(JobRecord job)
    {
        await RunAsync("Pausing download...", async () =>
        {
            await _huggingFace!.PauseDownloadAsync(job);
            await RefreshDownloadHistoryAsync();
            await RefreshJobsAsync();
            SetStatus($"Pause requested: {job.Id}");
        });
    }

    private async Task StopSelectedDownloadAsync()
    {
        var job = SelectedDownloadJob();
        if (job is null) { SetStatus("Select a download history row first."); return; }
        await StopDownloadAsync(job);
    }

    private async Task StopDownloadAsync(JobRecord job)
    {
        await RunAsync("Stopping download...", async () =>
        {
            await _huggingFace!.StopDownloadAsync(job);
            await RefreshDownloadHistoryAsync();
            await RefreshJobsAsync();
            SetStatus($"Stop requested: {job.Id}");
        });
    }

    private async Task DeleteDownloadAsync(JobRecord job)
    {
        if (_stateStore is null || _huggingFace is null) return;
        var payload = HuggingFaceService.ParseDownloadPayload(job.PayloadJson);
        var activeText = _huggingFace.IsDownloadActive(job.Id)
            ? "\n\nThis download is active. It will be stopped before the history entry is deleted."
            : "";
        var result = ThemedMessageBox.Show(
            this,
            $"Delete this model download history entry?\n\n{DownloadJobDisplayName(job, payload)}{activeText}\n\nIncomplete partial files are deleted. Completed model files are kept.",
            "Delete model download",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        await RunAsync("Deleting model download...", async () =>
        {
            if (_huggingFace.IsDownloadActive(job.Id))
            {
                await _huggingFace.StopDownloadAsync(job);
                await WaitForDownloadToStopAsync(job.Id);
                if (_huggingFace.IsDownloadActive(job.Id))
                {
                    SetStatus("Stop is still in progress. Try Delete again after the download stops.");
                    await RefreshDownloadHistoryAsync();
                    return;
                }
            }
            else if (job.Status is JobStatus.Queued or JobStatus.Running or JobStatus.Paused)
            {
                await _huggingFace.StopDownloadAsync(job);
            }

            DeleteDownloadPartialFile(payload);
            await _stateStore.DeleteJobAsync(job.Id);
            await RefreshDownloadHistoryAsync();
            await RefreshJobsAsync();
            SetStatus($"Deleted download history entry {job.Id}.");
        });
    }

    private async void ResumeDownloadRow_Click(object sender, RoutedEventArgs e)
    {
        await RunEventAsync(async () =>
        {
            var job = JobFromRowButton(sender);
            if (job is not null) await ResumeDownloadAsync(job);
        });
    }

    private async void PauseDownloadRow_Click(object sender, RoutedEventArgs e)
    {
        await RunEventAsync(async () =>
        {
            var job = JobFromRowButton(sender);
            if (job is not null) await PauseDownloadAsync(job);
        });
    }

    private async void StopDownloadRow_Click(object sender, RoutedEventArgs e)
    {
        await RunEventAsync(async () =>
        {
            var job = JobFromRowButton(sender);
            if (job is not null) await StopDownloadAsync(job);
        });
    }

    private async void DeleteDownloadRow_Click(object sender, RoutedEventArgs e)
    {
        await RunEventAsync(async () =>
        {
            var job = JobFromRowButton(sender);
            if (job is not null) await DeleteDownloadAsync(job);
        });
    }

    private void OpenRuntimeJobLogRow_Click(object sender, RoutedEventArgs e)
    {
        var job = JobFromRowButton(sender);
        if (job is null) return;
        if (!TryValidateLogFileForOpen(job.LogPath, out var error))
        {
            SetStatus(error);
            return;
        }

        Process.Start(new ProcessStartInfo(job.LogPath) { UseShellExecute = true });
    }

    private async Task MonitorDownloadAsync(string jobId)
    {
        while (_huggingFace?.IsDownloadActive(jobId) == true && !await DownloadJobReachedTerminalStatusAsync(jobId))
            await Task.Delay(1500);

        var refresh = await Dispatcher.InvokeAsync(RefreshCompletedDownloadAsync);
        await refresh;
    }

    private async Task<bool> DownloadJobReachedTerminalStatusAsync(string jobId)
    {
        if (_stateStore is null) return false;
        var job = (await _stateStore.ListJobsAsync()).FirstOrDefault(job => string.Equals(job.Id, jobId, StringComparison.OrdinalIgnoreCase));
        return job?.Status is JobStatus.Completed or JobStatus.Failed or JobStatus.Cancelled or JobStatus.Paused or JobStatus.Interrupted;
    }

    private async Task RefreshCompletedDownloadAsync()
    {
        if (_catalog is not null)
            await _catalog.ScanAsync(_settings.ModelsRoot);
        await RefreshModelsAsync();
        await RefreshJobsAsync();
        await RefreshOverviewAsync();
        await RefreshDownloadHistoryAsync();
        await RefreshHuggingFaceInstallStateAsync();
    }

    private void ConfigureHfSearchGrid()
    {
        if (_hfGrid is null) return;
        StopDownloadHistoryRefreshTimer();
        _hfShowingDownloadHistory = false;
        _downloadHistoryGrid = null;
        ConfigureGridColumns(_hfGrid, ("Repo", "C1", 1.3), ("File", "C2", 2.3), ("Quant", "C3", .6), ("Size", "C4", .8), ("Downloads", "C5", .8), ("Signals", "C6", 1.4));
        AddButtonColumn(_hfGrid, "Actions", "C7", "B1", DownloadHfRow_Click, .8, tooltipBinding: "T1");
        AddButtonColumn(_hfGrid, "Card", "C8", "B2", OpenHuggingFaceModelCardRow_Click, .6, tooltipBinding: "T2");
        ApplyGridTextMargin(_hfGrid, new Thickness(6, 0, 6, 0));
        SetHfSearchGridColumnSizing(_hfGrid);
        _hfGrid.SelectedItem = null;
        _hfGrid.ItemsSource = _viewModel.HuggingFace.SearchRows;
    }

    private void ConfigureDownloadHistoryGrid()
    {
        if (_hfGrid is null) return;
        _hfShowingDownloadHistory = true;
        _downloadHistoryGrid = _hfGrid;
        ConfigureGridColumns(_hfGrid, ("Status", "C1", .8), ("Model", "C2", 2.1), ("Progress", "C3", 1.1), ("Size", "C4", .8), ("Updated", "C5", 1), ("Destination", "C6", 2.4));
        AddButtonColumn(_hfGrid, "Start", "C7", "B1", ResumeDownloadRow_Click, .7, tooltipBinding: "T1");
        AddButtonColumn(_hfGrid, "Pause", "C8", "B2", PauseDownloadRow_Click, .7, tooltipBinding: "T2");
        AddButtonColumn(_hfGrid, "Stop", "C9", "B3", StopDownloadRow_Click, .7, tooltipBinding: "T3");
        AddButtonColumn(_hfGrid, "Delete", "C10", "B4", DeleteDownloadRow_Click, .7, tooltipBinding: "T4");
        ApplyGridTextMargin(_hfGrid, new Thickness(6, 0, 6, 0));
        SetDownloadHistoryGridColumnSizing(_hfGrid);
        _hfGrid.SelectedItem = null;
        _hfGrid.ItemsSource = _viewModel.HuggingFace.DownloadHistoryRows;
    }

    private void StartDownloadHistoryRefreshTimer()
    {
        _downloadHistoryTimer ??= new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        _downloadHistoryTimer.Tick -= DownloadHistoryTimer_Tick;
        _downloadHistoryTimer.Tick += DownloadHistoryTimer_Tick;
        _downloadHistoryTimer.Start();
    }

    private void StopDownloadHistoryRefreshTimer()
    {
        _downloadHistoryTimer?.Stop();
    }

    private async void DownloadHistoryTimer_Tick(object? sender, EventArgs e)
    {
        if (!_hfShowingDownloadHistory || _downloadHistoryRefreshInFlight) return;
        _downloadHistoryRefreshInFlight = true;
        try
        {
            await RefreshDownloadHistoryAsync();
        }
        catch (Exception ex)
        {
            SetStatus($"Download history refresh failed: {ex.Message}");
        }
        finally
        {
            _downloadHistoryRefreshInFlight = false;
        }
    }

    private async Task WaitForDownloadToStopAsync(string jobId)
    {
        for (var attempt = 0; attempt < 50 && _huggingFace?.IsDownloadActive(jobId) == true; attempt++)
            await Task.Delay(100);
    }

    private void DeleteDownloadPartialFile(DownloadJobPayload? payload)
    {
        if (payload is null || string.IsNullOrWhiteSpace(payload.Destination)) return;
        var destination = Path.GetFullPath(payload.Destination);
        var modelsRoot = Path.GetFullPath(_settings.ModelsRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var relative = Path.GetRelativePath(modelsRoot, destination);
        if (Path.IsPathRooted(relative)
            || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal)
            || string.Equals(relative, "..", StringComparison.Ordinal))
            return;

        var partial = destination + ".partial";
        if (File.Exists(partial))
            File.Delete(partial);

        var parent = Path.GetDirectoryName(destination);
        if (!string.IsNullOrWhiteSpace(parent)
            && FileSystemSafetyService.IsSafeChildDirectory(modelsRoot, parent)
            && Directory.Exists(parent)
            && !Directory.EnumerateFileSystemEntries(parent).Any())
            Directory.Delete(parent);
    }

    private static string DownloadJobDisplayName(JobRecord job, DownloadJobPayload? payload)
        => payload is null ? job.Id : $"{payload.File.Name}\n{payload.File.Repo}";
}
