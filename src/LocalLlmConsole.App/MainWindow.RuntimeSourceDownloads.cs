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
    private async Task DownloadRuntimeSourceAsync(RuntimeBuildPreset preset)
    {
        if (_jobs is null || _stateStore is null) return;
        var runtimes = await _stateStore.ListRuntimesAsync();
        var sources = RuntimeSources()
            .Where(source => string.Equals(source.PresetId, preset.Id, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(source => source.DownloadedAt)
            .ToList();
        var installed = runtimes
            .Where(runtime => string.Equals(RuntimeMetadataService.ManagedPresetId(runtime), preset.Id, StringComparison.OrdinalIgnoreCase))
            .GroupBy(runtime => RuntimeMetadataService.Folder(runtime), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(runtime => runtime.UpdatedAt).First())
            .OrderByDescending(runtime => runtime.UpdatedAt)
            .ToList();
        var localCommit = RuntimeBuildCatalogService.LatestLocalCommitValue(sources, installed);
        var updateState = CurrentRuntimeUpdateState(preset.Id, localCommit);
        if (!RuntimeBuildCatalogService.CanDownloadPreset(installed, sources, updateState))
        {
            ThemedMessageBox.Show(
                this,
                "This runtime source is already downloaded or built. Run Check to look for a newer remote commit, or delete the local source/build if you want to download from scratch.",
                "Download disabled",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            await RefreshRuntimesAsync();
            return;
        }

        var sourceDir = RuntimeSourceDir(preset);
        var job = await _jobs.CreateAsync("runtime-download", RuntimeBuildJobService.Payload(preset, "download", sourceDir, "Queued."));
        await RefreshJobsAsync();

        await RunAsync($"Downloading {preset.Label}...", async () =>
        {
            try
            {
                await UpdateRuntimeJobAsync(job, JobStatus.Running, preset, "download", sourceDir, "Downloading repository source...");
                Directory.CreateDirectory(RuntimeSourceRoot());
                await CloneOrUpdateRuntimeSourceAsync(preset, sourceDir, job.LogPath);
                var commit = await GitCommitAsync(sourceDir);
                var source = new RuntimeSourceEntry(preset.Id, preset.Label, preset.RepoUrl, preset.Branch, preset.Cuda, sourceDir, commit, DateTimeOffset.UtcNow, RuntimeBuildCatalogService.BackendKey(preset));
                await File.WriteAllTextAsync(RuntimeBuildCatalogService.SourceMetadataPath(sourceDir), JsonSerializer.Serialize(source, new JsonSerializerOptions { WriteIndented = true }));
                await UpdateRuntimeJobAsync(job, JobStatus.Completed, preset, "download", sourceDir, $"{preset.Label} downloaded at {RuntimeMetadataService.ShortCommit(commit)}. It now appears under Installed local builds.");
                await RefreshRuntimesAsync();
                await RefreshOverviewAsync();
            }
            catch (Exception ex)
            {
                await UpdateRuntimeJobAsync(job, JobStatus.Failed, preset, "download", sourceDir, ex.Message);
                throw;
            }
        });
    }

    private async Task CheckRuntimePresetUpdateAsync(RuntimeBuildPreset preset, RuntimeBuildPresetRow? row)
    {
        if (_jobs is null || _stateStore is null) return;
        if (row is not null)
        {
            row.LocalStatus = "Checking...";
            row.LatestLocal = "Checking remote...";
            row.CheckAction = "Checking";
            row.CanCheck = false;
            _runtimeBuildGrid?.Items.Refresh();
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Background);
        }

        var local = await LatestLocalRuntimeVersionAsync(preset);
        if (string.IsNullOrWhiteSpace(local.Commit))
        {
            if (row is not null)
            {
                row.LatestLocal = "Cannot compare: local commit unavailable. Delete the local source/build and download again if you need to refresh metadata.";
                row.LocalStatus = "Version unknown";
                row.CheckAction = "Check";
                row.CanDownload = false;
                row.CanCheck = true;
                _runtimeBuildGrid?.Items.Refresh();
            }
            SetStatus("Local runtime version is unknown. Delete the local source/build before downloading again.");
            return;
        }

        var job = await _jobs.CreateAsync("runtime-update-check", RuntimeBuildJobService.Payload(preset, "check", local.Path, "Queued."));
        await RefreshJobsAsync();
        await RunAsync($"Checking {preset.Label} for updates...", async () =>
        {
            try
            {
                await UpdateRuntimeJobAsync(job, JobStatus.Running, preset, "check", local.Path, "Checking remote repository...");
                var remoteCommit = await RemoteCommitAsync(preset);
                var message = RuntimeMetadataService.CommitsMatch(local.Commit, remoteCommit)
                    ? $"Already up to date at {RuntimeMetadataService.DisplayCommit(local.Commit)}."
                    : $"Update available: {RuntimeMetadataService.DisplayCommit(local.Commit)} -> {RuntimeMetadataService.DisplayCommit(remoteCommit)}. Use Download, then build the downloaded source.";
                _runtimeUpdateStates[preset.Id] = new RuntimeUpdateState(!RuntimeMetadataService.CommitsMatch(local.Commit, remoteCommit), local.Commit, remoteCommit, DateTimeOffset.UtcNow);
                await UpdateRuntimeJobAsync(job, JobStatus.Completed, preset, "check", local.Path, message);
                if (row is not null)
                {
                    var checkedState = _runtimeUpdateStates[preset.Id];
                    var currentSources = RuntimeSources()
                        .Where(source => string.Equals(source.PresetId, preset.Id, StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(source => source.DownloadedAt)
                        .ToList();
                    var currentInstalled = (await _stateStore.ListRuntimesAsync())
                        .Where(runtime => string.Equals(RuntimeMetadataService.ManagedPresetId(runtime), preset.Id, StringComparison.OrdinalIgnoreCase))
                        .GroupBy(runtime => RuntimeMetadataService.Folder(runtime), StringComparer.OrdinalIgnoreCase)
                        .Select(group => group.OrderByDescending(runtime => runtime.UpdatedAt).First())
                        .OrderByDescending(runtime => runtime.UpdatedAt)
                        .ToList();
                    row.LocalStatus = _runtimeUpdateStates[preset.Id].HasUpdate ? "Update available" : "Up to date";
                    row.LatestLocal = message;
                    row.CheckAction = "Check";
                    row.CanCheck = true;
                    row.DownloadAction = RuntimeBuildCatalogService.DownloadButtonLabel(currentSources, currentInstalled, checkedState);
                    row.CanDownload = RuntimeBuildCatalogService.CanDownloadPreset(currentInstalled, currentSources, checkedState);
                    _runtimeBuildGrid?.Items.Refresh();
                }
                ThemedMessageBox.Show(this, message, "Runtime update check", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                await UpdateRuntimeJobAsync(job, JobStatus.Failed, preset, "check", local.Path, ex.Message);
                if (row is not null)
                {
                    row.LocalStatus = "Check failed";
                    row.LatestLocal = $"Check failed: {ex.Message}";
                    row.CheckAction = "Check";
                    row.CanCheck = true;
                    _runtimeBuildGrid?.Items.Refresh();
                }
                throw;
            }
        });
        await RefreshRuntimesAsync();
    }
}
