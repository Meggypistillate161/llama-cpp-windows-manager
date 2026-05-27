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
    private async Task BuildRuntimeSourceAsync(RuntimeSourceEntry source)
    {
        var preset = RuntimeBuildPresetRows().FirstOrDefault(candidate => string.Equals(candidate.Id, source.PresetId, StringComparison.OrdinalIgnoreCase))
            ?? new RuntimeBuildPreset(source.PresetId, source.Label, source.RepoUrl, source.Branch, source.Cuda, Backend: source.Backend, Mode: source.Mode);
        await BuildManagedRuntimeAsync(preset, update: false, source);
    }

    private async Task DeleteRuntimeSourceAsync(RuntimeSourceEntry source)
    {
        if (!IsSafeRuntimeFolder(source.SourceDir))
        {
            SetStatus("Only downloaded sources inside the configured runtimes folder can be deleted from here.");
            return;
        }

        if (ThemedMessageBox.Show(this, $"Delete downloaded source?\n\n{source.Label}\n{source.SourceDir}", "Delete downloaded source", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await RunAsync("Deleting downloaded source...", async () =>
        {
            DeleteSafeRuntimeFolder(source.SourceDir);
            await RefreshRuntimesAsync();
            await RefreshOverviewAsync();
        });
    }

    private async Task DeleteAllRuntimePresetBuildsAsync(RuntimeBuildPreset preset)
    {
        if (_stateStore is null) return;
        var runtimes = (await _stateStore.ListRuntimesAsync())
            .Where(runtime => string.Equals(RuntimeMetadataService.ManagedPresetId(runtime), preset.Id, StringComparison.OrdinalIgnoreCase))
            .GroupBy(runtime => RuntimeMetadataService.Folder(runtime), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(runtime => runtime.UpdatedAt).First())
            .ToList();
        var sources = RuntimeSources()
            .Where(source => string.Equals(source.PresetId, preset.Id, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var sourceFolders = new HashSet<string>(sources.Select(source => source.SourceDir), StringComparer.OrdinalIgnoreCase);
        var defaultSourceDir = RuntimeSourceDir(preset);
        var hasPartialSourceCache = Directory.Exists(defaultSourceDir) && sourceFolders.Add(defaultSourceDir);

        if (runtimes.Count == 0 && sourceFolders.Count == 0)
        {
            if (!preset.Custom)
            {
                SetStatus("No local builds or downloaded sources for that repository.");
                return;
            }

            if (ThemedMessageBox.Show(this, $"Remove this custom repository from the list?\n\n{preset.Label}\n{preset.RepoUrl}", "Remove custom repository", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            await RemoveCustomRuntimeRepositoryAsync(preset);
            await RefreshRuntimesAsync();
            return;
        }

        var activeRuntimeIds = _sessions.Snapshots()
            .Where(session => session.IsRunning)
            .Select(session => session.RuntimeId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (runtimes.Any(runtime => activeRuntimeIds.Contains(runtime.Id)))
        {
            SetStatus("Unload the running model before deleting the runtime it is using.");
            return;
        }

        var customNote = preset.Custom ? "\n\nThis will also remove the custom repository from the list." : "";
        var partialNote = hasPartialSourceCache ? "\nPartial source cache: 1" : "";
        var message = $"Delete all local builds and downloaded sources from this repository?\n\n{preset.Label}\n\nBuilt runtimes: {runtimes.Count}\nDownloaded sources: {sources.Count}{partialNote}{customNote}";
        if (ThemedMessageBox.Show(this, message, "Delete all runtime builds", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await RunAsync("Deleting repository builds...", async () =>
        {
            foreach (var runtime in runtimes)
            {
                await _stateStore.DeleteRuntimeAsync(runtime.Id);
                if (CanDeleteRuntimeFiles(runtime, out var folder, out _))
                    DeleteRuntimeFiles(folder);
            }

            foreach (var sourceDir in sourceFolders)
            {
                if (Directory.Exists(sourceDir) && IsSafeRuntimeFolder(sourceDir))
                    DeleteSafeRuntimeFolder(sourceDir);
            }

            if (preset.Custom)
                await RemoveCustomRuntimeRepositoryAsync(preset);

            await RefreshRuntimesAsync();
            await RefreshOverviewAsync();
        });
    }

    private async Task RemoveCustomRuntimeRepositoryAsync(RuntimeBuildPreset preset)
    {
        var customPresets = CustomRuntimeBuildPresets()
            .Where(candidate => !string.Equals(candidate.Id, preset.Id, StringComparison.OrdinalIgnoreCase))
            .ToList();
        await SaveCustomRuntimeBuildPresetsAsync(customPresets);
    }

    private async Task BuildManagedRuntimeAsync(RuntimeBuildPreset preset, bool update, RuntimeSourceEntry? source = null)
    {
        var script = await EnsureBuildToolScriptAsync();
        if (_jobs is null || _runtimes is null) return;
        var backend = RuntimeBuildCatalogService.BuildBackend(preset);
        var mode = RuntimeBuildCatalogService.BuildMode(preset);
        if (mode == RuntimeMode.Wsl)
        {
            await EnsureWslDistroReadyAsync(_settings.WslDistro);
            if (backend == RuntimeBackend.Cuda)
                await EnsureWslCudaToolkitReadyAsync(_settings.WslDistro);
            if (backend == RuntimeBackend.Vulkan)
                await EnsureWslVulkanToolsReadyAsync(_settings.WslDistro);
            if (backend == RuntimeBackend.Sycl)
                await EnsureWslSyclToolsReadyAsync(_settings.WslDistro);
        }
        else
        {
            EnsureWindowsBuildToolsReady(backend);
        }

        Directory.CreateDirectory(_settings.RuntimeRoot);
        Directory.CreateDirectory(_settings.CacheRoot);
        var plan = RuntimeBuildJobService.CreatePlan(preset, update, source, _settings, DateTimeOffset.UtcNow);
        var payloadSourceDir = source?.SourceDir ?? "";
        var job = await _jobs.CreateAsync("runtime-build", RuntimeBuildJobService.Payload(preset, plan.Action, plan.InstallDir, plan.QueuedMessage, plan.ProcessMarker, _settings.WslDistro, payloadSourceDir));
        var buildCancellation = RegisterRuntimeBuildCancellation(job.Id);
        await RefreshJobsAsync();

        await RunAsync($"{(update ? "Updating" : "Building")} {preset.Label}...", async () =>
        {
            try
            {
                buildCancellation.Token.ThrowIfCancellationRequested();
                await UpdateRuntimeJobAsync(job, JobStatus.Running, preset, plan.Action, plan.InstallDir, update ? "Checking remote repository..." : "Building downloaded source...", plan.ProcessMarker, payloadSourceDir);
                if (update)
                {
                    var check = await CheckRuntimeUpdateAsync(preset);
                    buildCancellation.Token.ThrowIfCancellationRequested();
                    if (check.IsInstalled && !check.HasUpdate)
                    {
                        var message = $"Already up to date at {check.LocalCommit}. No new build was created.";
                        await UpdateRuntimeJobAsync(job, JobStatus.Completed, preset, "update", plan.InstallDir, message, plan.ProcessMarker, payloadSourceDir);
                        await RefreshOverviewAsync();
                        ThemedMessageBox.Show(this, message, "Runtime update", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    if (check.IsInstalled && check.HasUpdate)
                    {
                        await UpdateRuntimeJobAsync(job, JobStatus.Running, preset, "update", plan.InstallDir, $"Update found: {RuntimeMetadataService.ShortCommit(check.LocalCommit)} -> {RuntimeMetadataService.ShortCommit(check.RemoteCommit)}. Building new runtime...", plan.ProcessMarker, payloadSourceDir);
                    }
                    else
                    {
                        await UpdateRuntimeJobAsync(job, JobStatus.Running, preset, "update", plan.InstallDir, "No installed build was detected. Building a fresh runtime...", plan.ProcessMarker, payloadSourceDir);
                    }
                }

                await RunRuntimeBuildToolAsync(script, plan.SourceDir, plan.BuildDir, plan.InstallDir, preset, job.LogPath, source is not null, plan.ProcessMarker, buildCancellation.Token);
                await RuntimeBuildJobService.StampManagedMetadataAsync(plan.InstallDir, preset, update);
                await _runtimes.RegisterFolderAsync(plan.InstallDir);
                var cleanupMessage = source is null
                    ? ""
                    : await TryDeleteRuntimeSourceAfterSuccessfulBuildAsync(source, job.LogPath);
                await UpdateRuntimeJobAsync(job, JobStatus.Completed, preset, plan.Action, plan.InstallDir, $"{preset.Label} installed as {Path.GetFileName(plan.InstallDir)}.{cleanupMessage}", plan.ProcessMarker, payloadSourceDir);
                await RefreshRuntimesAsync();
                await RefreshOverviewAsync();
                SetStatus($"{preset.Label} installed as {Path.GetFileName(plan.InstallDir)}.{cleanupMessage}");
            }
            catch (Exception ex)
            {
                if (buildCancellation.IsCancellationRequested)
                {
                    await UpdateRuntimeJobAsync(job, JobStatus.Cancelled, preset, plan.Action, plan.InstallDir, "Cancelled by user.", plan.ProcessMarker, payloadSourceDir);
                    SetStatus($"Cancelled {preset.Label} build.");
                    return;
                }

                await UpdateRuntimeJobAsync(job, JobStatus.Failed, preset, plan.Action, plan.InstallDir, ex.Message, plan.ProcessMarker, payloadSourceDir);
                throw;
            }
            finally
            {
                UnregisterRuntimeBuildCancellation(job.Id, buildCancellation);
            }
        });
    }

    private async Task UpdateRuntimeJobAsync(JobRecord job, JobStatus status, RuntimeBuildPreset preset, string action, string installDir, string message, string processMarker = "", string sourceDir = "")
    {
        if (_jobs is null) return;
        await RuntimeBuildJobService.AppendJobLogAsync(job.LogPath, status, message, MaxLogBytes());
        await _jobs.UpdateAsync(job, status, RuntimeBuildJobService.Payload(preset, action, installDir, message, processMarker, _settings.WslDistro, sourceDir));
        await RefreshJobsAsync();
    }

    private async Task<string> TryDeleteRuntimeSourceAfterSuccessfulBuildAsync(RuntimeSourceEntry source, string logPath)
    {
        if (!_settings.DeleteRuntimeSourceAfterSuccessfulBuild) return "";
        if (string.IsNullOrWhiteSpace(source.SourceDir) || !Directory.Exists(source.SourceDir))
            return " Downloaded source was already absent.";

        try
        {
            DeleteSafeRuntimeFolder(source.SourceDir);
            var message = $"Deleted downloaded source after successful build: {source.SourceDir}";
            await BoundedLogFile.AppendAsync(logPath, $"[{DateTimeOffset.Now:O}] Completed: {message}{Environment.NewLine}", MaxLogBytes());
            return " Downloaded source deleted.";
        }
        catch (Exception ex)
        {
            var message = $"Downloaded source cleanup failed: {ex.Message}";
            await BoundedLogFile.AppendAsync(logPath, $"[{DateTimeOffset.Now:O}] Warning: {message}{Environment.NewLine}", MaxLogBytes());
            return $" {message}";
        }
    }
}
