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
    private async void CancelRuntimeJobRow_Click(object sender, RoutedEventArgs e)
    {
        await RunEventAsync(async () =>
        {
            var job = JobFromRowButton(sender);
            if (job is not null) await CancelRuntimeBuildJobAsync(job);
        });
    }

    private async void RetryRuntimeJobRow_Click(object sender, RoutedEventArgs e)
    {
        await RunEventAsync(async () =>
        {
            var job = JobFromRowButton(sender);
            if (job is not null) await RetryRuntimeBuildJobAsync(job);
        });
    }

    private async void ClearRuntimeJobRow_Click(object sender, RoutedEventArgs e)
    {
        await RunEventAsync(async () =>
        {
            var job = JobFromRowButton(sender);
            if (job is not null) await ClearRuntimeBuildJobAsync(job);
        });
    }

    private CancellationTokenSource RegisterRuntimeBuildCancellation(string jobId)
    {
        var cancellation = new CancellationTokenSource();
        lock (_runtimeBuildCancellations)
            _runtimeBuildCancellations[jobId] = cancellation;
        return cancellation;
    }

    private bool TryCancelRuntimeBuild(string jobId)
    {
        lock (_runtimeBuildCancellations)
        {
            if (!_runtimeBuildCancellations.TryGetValue(jobId, out var cancellation)) return false;
            cancellation.Cancel();
            return true;
        }
    }

    private void UnregisterRuntimeBuildCancellation(string jobId, CancellationTokenSource cancellation)
    {
        lock (_runtimeBuildCancellations)
        {
            if (_runtimeBuildCancellations.TryGetValue(jobId, out var current) && ReferenceEquals(current, cancellation))
                _runtimeBuildCancellations.Remove(jobId);
        }
        cancellation.Dispose();
    }

    private async Task CancelRuntimeBuildJobAsync(JobRecord job)
    {
        if (_jobs is null) return;
        var payload = RuntimeBuildJobService.ParsePayload(job.PayloadJson);
        if (!RuntimeBuildJobService.CanCancel(job) || payload is null)
        {
            SetStatus("Only active runtime build jobs can be cancelled.");
            return;
        }

        TryCancelRuntimeBuild(job.Id);
        if (payload.Mode == RuntimeMode.Wsl && !string.IsNullOrWhiteSpace(payload.ProcessMarker))
            await CleanupWslBuildMarkerAsync(string.IsNullOrWhiteSpace(payload.WslDistro) ? _settings.WslDistro : payload.WslDistro, payload.ProcessMarker);

        await RuntimeBuildJobService.AppendJobLogAsync(job.LogPath, JobStatus.Cancelled, "Cancel requested by user.", MaxLogBytes());
        await _jobs.UpdateAsync(job, JobStatus.Cancelled, RuntimeBuildJobService.Payload(payload.Preset, payload.Action, payload.InstallDir, "Cancel requested by user.", payload.ProcessMarker, payload.WslDistro, payload.SourceDir));
        await RefreshJobsAsync();
        SetStatus($"Cancel requested for {payload.Preset.Label}.");
    }

    private async Task RetryRuntimeBuildJobAsync(JobRecord job)
    {
        var payload = RuntimeBuildJobService.ParsePayload(job.PayloadJson);
        if (!RuntimeBuildJobService.CanRetry(job) || payload is null)
        {
            SetStatus("Only failed, cancelled, or interrupted runtime build jobs can be retried.");
            return;
        }

        await BuildManagedRuntimeAsync(
            payload.Preset,
            payload.Action.Equals("update", StringComparison.OrdinalIgnoreCase),
            RuntimeSourceFromBuildPayload(payload));
    }

    private static RuntimeSourceEntry? RuntimeSourceFromBuildPayload(RuntimeBuildJobPayload payload)
    {
        if (string.IsNullOrWhiteSpace(payload.SourceDir) || !Directory.Exists(payload.SourceDir))
            return null;

        var commit = RuntimeMetadataService.TryReadGitHeadCommit(payload.SourceDir);
        if (string.IsNullOrWhiteSpace(commit))
            commit = RuntimeMetadataService.InferCommitFromText(payload.SourceDir);
        return new RuntimeSourceEntry(
            payload.Preset.Id,
            payload.Preset.Label,
            payload.Preset.RepoUrl,
            payload.Preset.Branch,
            payload.Preset.Cuda,
            payload.SourceDir,
            commit,
            DateTimeOffset.UtcNow,
            RuntimeBuildCatalogService.BackendKey(payload.Preset),
            payload.Mode);
    }

    private async Task ClearRuntimeBuildJobAsync(JobRecord job)
    {
        if (_stateStore is null) return;
        if (!RuntimeBuildJobService.CanClear(job))
        {
            SetStatus("Only completed, failed, cancelled, or interrupted runtime jobs can be cleared.");
            return;
        }

        if (ThemedMessageBox.Show(this, $"Clear this runtime job and its log?\n\n{job.Id}", "Clear runtime job", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        await RunAsync("Clearing runtime job...", async () =>
        {
            await _stateStore.DeleteJobAsync(job.Id);
            if (LogFileService.TryValidateWorkspaceLogFile(_workspaceRoot, job.LogPath, out var fullPath, out _))
                LogFileService.DeleteLogs([fullPath]);

            await RefreshJobsAsync();
            SetStatus($"Cleared runtime job {job.Id}.");
        });
    }
}
