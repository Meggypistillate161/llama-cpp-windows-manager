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
    private async Task InstallRuntimePackageAsync(RuntimePackagePreset preset)
    {
        if (_jobs is null || _runtimes is null || _stateStore is null) return;
        if (preset.Mode == RuntimeMode.Wsl)
            await EnsureWslDistroReadyAsync(_settings.WslDistro);

        var allRuntimes = await _stateStore.ListRuntimesAsync();
        var existing = RuntimePackageCatalogService.InstalledPackages(allRuntimes, preset);
        var sourceBuilds = RuntimePackageCatalogService.MatchingSourceBuilds(allRuntimes, preset);
        var localIdentity = RuntimePackageCatalogService.LocalIdentity(existing, sourceBuilds);
        var updateState = _runtimePackageUpdateStates.TryGetValue(preset.Id, out var state)
            && string.Equals(state.LocalIdentity ?? "", localIdentity ?? "", StringComparison.OrdinalIgnoreCase)
                ? state
                : null;
        if (!RuntimePackageCatalogService.CanInstallPackage(existing, sourceBuilds, updateState))
        {
            SetStatus("This official prebuilt runtime is already installed. Run Check to look for a newer release, or delete it before reinstalling.");
            await RefreshRuntimesAsync();
            return;
        }

        Directory.CreateDirectory(_settings.RuntimeRoot);
        Directory.CreateDirectory(_settings.CacheRoot);
        var job = await _jobs.CreateAsync("runtime-package-download", RuntimePackageJobPayload(preset, "install", "", "Queued."));
        await RefreshJobsAsync();

        await RunAsync($"Installing {preset.Label}...", async () =>
        {
            var installDir = "";
            try
            {
                await UpdateRuntimePackageJobAsync(job, JobStatus.Running, preset, "install", "", "Resolving latest official llama.cpp release...");
                var release = await RuntimePackageCatalogService.FetchLatestReleaseAsync(_runtimePackageClient);
                var selection = RuntimePackageCatalogService.SelectAssets(preset, release);
                installDir = RuntimePackageCatalogService.InstallDir(_settings.RuntimeRoot, selection);
                var cacheDir = RuntimePackageCatalogService.DownloadCacheDir(_settings.CacheRoot, selection);
                if (Directory.Exists(installDir))
                {
                    if (!IsSafeRuntimeFolder(installDir))
                        throw new InvalidOperationException("Refusing to replace a runtime package outside the configured runtimes folder.");
                    DeleteSafeRuntimeFolder(installDir);
                }
                Directory.CreateDirectory(installDir);
                Directory.CreateDirectory(cacheDir);

                foreach (var asset in selection.AllAssets)
                {
                    var archivePath = Path.Combine(cacheDir, asset.Name);
                    var sizeText = asset.SizeBytes > 0 ? $" ({DisplayFormatService.Bytes(asset.SizeBytes)})" : "";
                    await UpdateRuntimePackageJobAsync(job, JobStatus.Running, preset, "install", installDir, $"Downloading {asset.Name}{sizeText}...");
                    await RuntimePackageCatalogService.DownloadAssetAsync(_runtimePackageClient, asset, archivePath);
                    await RuntimeBuildJobService.AppendJobLogAsync(job.LogPath, JobStatus.Running, $"Extracting {asset.Name}...", MaxLogBytes());
                    if (preset.Mode == RuntimeMode.Wsl && IsTarArchive(archivePath))
                        await ExtractWslRuntimeArchiveAsync(archivePath, installDir, job.LogPath);
                    else
                        RuntimePackageCatalogService.ExtractArchive(archivePath, installDir);
                }

                var executable = RuntimePackageCatalogService.FindRuntimeExecutable(installDir, preset.Mode);
                var runtimeFolder = RuntimePackageCatalogService.RuntimeFolderFromExecutable(executable);
                await RuntimePackageCatalogService.StampManagedMetadataAsync(runtimeFolder, installDir, selection);
                if (!string.Equals(runtimeFolder, installDir, StringComparison.OrdinalIgnoreCase))
                    await RuntimePackageCatalogService.StampManagedMetadataAsync(installDir, installDir, selection);
                await TryPrepareWslRuntimeExecutableAsync(preset, executable, job.LogPath);
                await _runtimes.RegisterFolderAsync(runtimeFolder);
                _runtimePackageUpdateStates[preset.Id] = new RuntimePackageUpdateState(false, selection.ReleaseTag, selection.ReleaseTag, selection.ReleaseUrl, selection.AssetSummary, DateTimeOffset.UtcNow, $"package:{selection.ReleaseTag}", release.TargetCommit);
                await UpdateRuntimePackageJobAsync(job, JobStatus.Completed, preset, "install", runtimeFolder, $"{preset.Label} installed from {selection.ReleaseTag}.");
                await RefreshRuntimesAsync();
                await RefreshOverviewAsync();
                SetStatus($"{preset.Label} installed from {selection.ReleaseTag}.");
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(installDir) && Directory.Exists(installDir) && IsSafeRuntimeFolder(installDir))
                {
                    try { DeleteSafeRuntimeFolder(installDir); }
                    catch { }
                }
                await UpdateRuntimePackageJobAsync(job, JobStatus.Failed, preset, "install", installDir, ex.Message);
                throw;
            }
        });
    }

    private async Task CheckRuntimePackageUpdateAsync(RuntimePackagePreset preset, RuntimePackagePresetRow? row)
    {
        if (_jobs is null || _stateStore is null) return;
        if (row is not null)
        {
            row.LocalStatus = "Checking...";
            row.LatestRelease = "Checking release...";
            row.CheckAction = "Checking";
            row.CanCheck = false;
            _runtimePackageGrid?.Items.Refresh();
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Background);
        }

        var allRuntimes = await _stateStore.ListRuntimesAsync();
        var installed = RuntimePackageCatalogService.InstalledPackages(allRuntimes, preset);
        var sourceBuilds = RuntimePackageCatalogService.MatchingSourceBuilds(allRuntimes, preset);
        var localTag = RuntimePackageCatalogService.LatestInstalledTag(installed);
        var sourceCommit = RuntimePackageCatalogService.LatestSourceCommit(sourceBuilds);
        var localIdentity = RuntimePackageCatalogService.LocalIdentity(installed, sourceBuilds);
        var job = await _jobs.CreateAsync("runtime-package-update-check", RuntimePackageJobPayload(preset, "check", "", "Queued."));
        await RefreshJobsAsync();

        await RunAsync($"Checking {preset.Label} release...", async () =>
        {
            RuntimePackageRelease? release = null;
            try
            {
                await UpdateRuntimePackageJobAsync(job, JobStatus.Running, preset, "check", "", "Checking official llama.cpp release assets...");
                release = await RuntimePackageCatalogService.FetchLatestReleaseAsync(_runtimePackageClient);
                var selection = RuntimePackageCatalogService.SelectAssets(preset, release);
                var hasPackageTag = !string.IsNullOrWhiteSpace(localTag);
                var hasUpdate = hasPackageTag
                    ? !string.Equals(localTag, selection.ReleaseTag, StringComparison.OrdinalIgnoreCase)
                    : sourceBuilds.Count > 0
                        && !RuntimeMetadataService.CommitsMatch(sourceCommit, release.TargetCommit);
                var message = installed.Count == 0 && sourceBuilds.Count == 0
                    ? $"Latest available release is {selection.ReleaseTag}. Use Install to download the official prebuilt runtime."
                    : installed.Count == 0
                        ? hasUpdate
                            ? $"Source build found at {RuntimeMetadataService.DisplayCommit(sourceCommit)}. Latest prebuilt release is {selection.ReleaseTag} ({RuntimeMetadataService.DisplayCommit(release.TargetCommit)})."
                            : $"Source build matches the latest release commit {RuntimeMetadataService.DisplayCommit(release.TargetCommit)}. Install the prebuilt package only if you want exact binary verification."
                    : hasUpdate
                        ? $"Update available: {RuntimePackageCatalogService.DisplayTag(localTag)} -> {selection.ReleaseTag}. Use Update to install the new prebuilt runtime."
                        : $"Already current at {selection.ReleaseTag}.";

                _runtimePackageUpdateStates[preset.Id] = new RuntimePackageUpdateState(hasUpdate, localTag, selection.ReleaseTag, selection.ReleaseUrl, selection.AssetSummary, DateTimeOffset.UtcNow, localIdentity, release.TargetCommit);
                await UpdateRuntimePackageJobAsync(job, JobStatus.Completed, preset, "check", "", message);
                if (row is not null)
                {
                    var checkedState = _runtimePackageUpdateStates[preset.Id];
                    row.LocalStatus = hasUpdate ? "Update available" : RuntimePackageCatalogService.LocalStatusLabel(installed, sourceBuilds, checkedState);
                    row.LatestRelease = RuntimePackageCatalogService.LatestLocalLabel(installed, sourceBuilds, checkedState);
                    row.Assets = checkedState.AssetSummary;
                    row.InstallAction = RuntimePackageCatalogService.InstallButtonLabel(installed, sourceBuilds, checkedState);
                    row.CanInstall = RuntimePackageCatalogService.CanInstallPackage(installed, sourceBuilds, checkedState);
                    row.CheckAction = "Check";
                    row.CanCheck = true;
                    _runtimePackageGrid?.Items.Refresh();
                }

                ThemedMessageBox.Show(this, message, "Runtime download check", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (RuntimePackageAssetUnavailableException ex)
            {
                var latestTag = release?.TagName ?? "";
                var releaseUrl = release?.HtmlUrl ?? RuntimePackageCatalogService.ReleasesUrl;
                _runtimePackageUpdateStates[preset.Id] = new RuntimePackageUpdateState(false, localTag, latestTag, releaseUrl, ex.Message, DateTimeOffset.UtcNow, localIdentity, release?.TargetCommit ?? "", IsAvailable: false);
                await UpdateRuntimePackageJobAsync(job, JobStatus.Completed, preset, "check", "", ex.Message);
                if (row is not null)
                {
                    var checkedState = _runtimePackageUpdateStates[preset.Id];
                    row.LocalStatus = RuntimePackageCatalogService.LocalStatusLabel(installed, sourceBuilds, checkedState);
                    row.LatestRelease = RuntimePackageCatalogService.LatestLocalLabel(installed, sourceBuilds, checkedState);
                    row.Assets = checkedState.AssetSummary;
                    row.InstallAction = RuntimePackageCatalogService.InstallButtonLabel(installed, sourceBuilds, checkedState);
                    row.CanInstall = RuntimePackageCatalogService.CanInstallPackage(installed, sourceBuilds, checkedState);
                    row.CheckAction = "Check";
                    row.CanCheck = true;
                    _runtimePackageGrid?.Items.Refresh();
                }
                ThemedMessageBox.Show(this, ex.Message, "Runtime download check", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                await UpdateRuntimePackageJobAsync(job, JobStatus.Failed, preset, "check", "", ex.Message);
                if (row is not null)
                {
                    row.LocalStatus = "Check failed";
                    row.LatestRelease = $"Check failed: {ex.Message}";
                    row.CheckAction = "Check";
                    row.CanCheck = true;
                    _runtimePackageGrid?.Items.Refresh();
                }
                throw;
            }
        });
        await RefreshRuntimesAsync();
    }

    private async Task DeleteRuntimePackageBuildsAsync(RuntimePackagePreset preset)
    {
        if (_stateStore is null) return;
        var allRuntimes = await _stateStore.ListRuntimesAsync();
        var runtimes = allRuntimes
            .Where(runtime => string.Equals(RuntimeMetadataService.ManagedPackageId(runtime), preset.Id, StringComparison.OrdinalIgnoreCase)
                || RuntimeMetadataService.EquivalentPackageIds(runtime).Contains(preset.Id, StringComparer.OrdinalIgnoreCase)
                || string.Equals(RuntimeMetadataService.ManagedPresetId(runtime), preset.SourcePresetId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (runtimes.Count == 0)
        {
            SetStatus("No local installs for that official prebuilt runtime package.");
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

        var modelsByRuntime = await ModelsByRuntimeAsync();
        var usedByModels = runtimes
            .Where(runtime => modelsByRuntime.TryGetValue(runtime.Id, out var models) && models.Count > 0)
            .SelectMany(runtime => modelsByRuntime[runtime.Id])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (usedByModels.Count > 0)
        {
            SetStatus($"Update saved launch settings before deleting this runtime package. Used by: {string.Join(", ", usedByModels.Take(4))}.");
            return;
        }

        var folders = runtimes
            .Select(RuntimeMetadataService.Folder)
            .Where(folder => !string.IsNullOrWhiteSpace(folder))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (ThemedMessageBox.Show(this, $"Delete all local installs for this official prebuilt runtime?\n\n{preset.Label}\n\nInstalled runtimes: {folders.Count}", "Delete runtime downloads", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        await RunAsync("Deleting runtime downloads...", async () =>
        {
            foreach (var runtime in runtimes)
                await _stateStore.DeleteRuntimeAsync(runtime.Id);

            foreach (var folder in folders)
            {
                if (Directory.Exists(folder) && IsSafeRuntimeFolder(folder))
                    DeleteSafeRuntimeFolder(folder);
            }

            _runtimePackageUpdateStates.Remove(preset.Id);
            await RefreshRuntimesAsync();
            await RefreshOverviewAsync();
            SetStatus($"Deleted local installs for {preset.Label}.");
        });
    }

    private async Task UpdateRuntimePackageJobAsync(JobRecord job, JobStatus status, RuntimePackagePreset preset, string action, string installDir, string message)
    {
        if (_jobs is null) return;
        await RuntimeBuildJobService.AppendJobLogAsync(job.LogPath, status, message, MaxLogBytes());
        await _jobs.UpdateAsync(job, status, RuntimePackageJobPayload(preset, action, installDir, message));
        await RefreshJobsAsync();
    }

    private static string RuntimePackageJobPayload(RuntimePackagePreset preset, string action, string installDir, string message) => JsonSerializer.Serialize(new
    {
        preset = preset.Id,
        label = preset.Label,
        backend = RuntimeBuildCatalogService.BackendKey(preset.Backend),
        mode = RuntimeBuildCatalogService.ModeKey(preset.Mode),
        sourcePresetId = preset.SourcePresetId,
        installDir,
        action,
        message
    });

    private async Task TryPrepareWslRuntimeExecutableAsync(RuntimePackagePreset preset, string executable, string logPath)
    {
        if (preset.Mode != RuntimeMode.Wsl || string.IsNullOrWhiteSpace(executable)) return;
        try
        {
            var psi = new ProcessStartInfo(HostExecutableResolver.WslExe())
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var arg in new[] { "-d", _settings.WslDistro, "--", "bash", "-lc", $"chmod +x {BashQuote(WindowsPathToWslPath(executable))}" })
                psi.ArgumentList.Add(arg);
            var result = await _processRunner.RunAsync(psi, TimeSpan.FromSeconds(15));
            if (!string.IsNullOrWhiteSpace(result.Output) || !string.IsNullOrWhiteSpace(result.Error))
                await BoundedLogFile.AppendAsync(logPath, result.Output + result.Error + Environment.NewLine, MaxLogBytes());
        }
        catch (Exception ex)
        {
            await RuntimeBuildJobService.AppendJobLogAsync(logPath, JobStatus.Running, $"Warning: could not chmod WSL runtime executable: {ex.Message}", MaxLogBytes());
        }
    }

    private async Task ExtractWslRuntimeArchiveAsync(string archivePath, string installDir, string logPath)
    {
        var psi = new ProcessStartInfo(HostExecutableResolver.WslExe())
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        var command = $"mkdir -p {BashQuote(WindowsPathToWslPath(installDir))} && tar --overwrite -xzf {BashQuote(WindowsPathToWslPath(archivePath))} -C {BashQuote(WindowsPathToWslPath(installDir))}";
        foreach (var arg in new[] { "-d", _settings.WslDistro, "--", "bash", "-lc", command })
            psi.ArgumentList.Add(arg);
        var result = await _processRunner.RunAsync(psi, TimeSpan.FromMinutes(10));
        if (!string.IsNullOrWhiteSpace(result.Output) || !string.IsNullOrWhiteSpace(result.Error))
            await BoundedLogFile.AppendAsync(logPath, result.Output + result.Error + Environment.NewLine, MaxLogBytes());
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"WSL archive extraction failed with exit code {result.ExitCode}: {result.Error}".Trim());
    }

    private static bool IsTarArchive(string path)
    {
        var name = Path.GetFileName(path);
        return name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase);
    }

    private static string WindowsPathToWslPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        if (value.StartsWith('/')) return value.Replace('\\', '/');
        var full = Path.GetFullPath(value);
        if (full.Length >= 3 && full[1] == ':' && (full[2] == '\\' || full[2] == '/'))
        {
            var drive = char.ToLowerInvariant(full[0]);
            var rest = full[3..].Replace('\\', '/');
            return $"/mnt/{drive}/{rest}";
        }

        return full.Replace('\\', '/');
    }

    private static string BashQuote(string value)
        => "'" + (value ?? "").Replace("'", "'\"'\"'") + "'";
}
