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
    private long MaxLogBytes() => BoundedLogFile.MegabytesToBytes(_settings.MaxLogFileSizeMb);

    private async Task CloneOrUpdateRuntimeSourceAsync(RuntimeBuildPreset preset, string sourceDir, string logPath)
    {
        if (!IsSafeRuntimeFolder(sourceDir))
            throw new InvalidOperationException("Refusing to write runtime source outside the configured runtimes folder.");

        if (Directory.Exists(sourceDir))
        {
            if (await TryPrepareExistingRuntimeSourceAsync(sourceDir, logPath))
            {
                await RunGitCommandAsync(sourceDir, logPath, MaxLogBytes(), "fetch", "--all", "--tags");
                if (!string.IsNullOrWhiteSpace(preset.Branch))
                    await RunGitCommandAsync(sourceDir, logPath, MaxLogBytes(), "checkout", preset.Branch);
                await RunGitCommandAsync(sourceDir, logPath, MaxLogBytes(), "pull", "--ff-only");
                return;
            }

            await RuntimeBuildJobService.AppendRecoveryLogAsync(logPath, $"Discarding incomplete runtime source folder before reclone: {sourceDir}", MaxLogBytes());
            DeleteSafeRuntimeFolder(sourceDir);
        }

        var args = new List<string> { "clone", "--depth", "1" };
        if (!string.IsNullOrWhiteSpace(preset.Branch))
            args.AddRange(["--branch", preset.Branch]);
        args.AddRange([preset.RepoUrl, sourceDir]);
        try
        {
            await RunGitCommandAsync(_settings.RuntimeRoot, logPath, MaxLogBytes(), args.ToArray());
        }
        catch (Exception ex)
        {
            await RuntimeBuildJobService.AppendRecoveryLogAsync(logPath, $"Runtime source clone failed and the incomplete folder will be removed before the next retry: {ex.Message}", MaxLogBytes());
            await TryDeleteIncompleteRuntimeSourceAsync(sourceDir, logPath);
            throw;
        }
    }

    private async Task<bool> TryPrepareExistingRuntimeSourceAsync(string sourceDir, string logPath)
    {
        if (!Directory.Exists(Path.Combine(sourceDir, ".git")) && !File.Exists(Path.Combine(sourceDir, ".git")))
            return false;

        try
        {
            var output = await RunGitCommandAsync(sourceDir, null, 0, "rev-parse", "--is-inside-work-tree");
            if (!string.Equals(output.Trim(), "true", StringComparison.OrdinalIgnoreCase))
                return false;

            var status = await RunGitCommandAsync(sourceDir, null, 0, "status", "--porcelain");
            if (string.IsNullOrWhiteSpace(status)) return true;

            await RuntimeBuildJobService.AppendRecoveryLogAsync(logPath, "Existing runtime source checkout is incomplete or dirty and will be repaired before updating.", MaxLogBytes());
            await RunGitCommandAsync(sourceDir, logPath, MaxLogBytes(), "checkout", "--force", "HEAD");
            status = await RunGitCommandAsync(sourceDir, null, 0, "status", "--porcelain");
            return string.IsNullOrWhiteSpace(status);
        }
        catch (Exception ex)
        {
            await RuntimeBuildJobService.AppendRecoveryLogAsync(logPath, $"Existing runtime source is not a valid git repository and will be recloned: {ex.Message}", MaxLogBytes());
            return false;
        }
    }

    private async Task<string> GitCommitAsync(string sourceDir)
    {
        var output = await RunGitCommandAsync(sourceDir, null, 0, "rev-parse", "--short=12", "HEAD");
        return output.Trim();
    }

    private async Task<string> RunGitCommandAsync(string workingDirectory, string? logPath, long maxLogBytes, params string[] args)
    {
        var psi = new ProcessStartInfo(HostExecutableResolver.GitExe())
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Directory.Exists(workingDirectory) ? workingDirectory : Environment.CurrentDirectory
        };
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add("core.longpaths=true");
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        var result = await _processRunner.RunAsync(psi, TimeSpan.FromMinutes(60));
        if (!string.IsNullOrWhiteSpace(logPath))
        {
            await BoundedLogFile.AppendAsync(logPath, $"> git -c core.longpaths=true {string.Join(' ', args.Select(RuntimeBuildJobService.RedactCommandArgument))}{Environment.NewLine}{result.Output}{result.Error}{Environment.NewLine}", maxLogBytes);
        }
        if (result.ExitCode != 0) throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error);
        return result.Output;
    }

    private async Task TryDeleteIncompleteRuntimeSourceAsync(string sourceDir, string logPath)
    {
        try
        {
            if (Directory.Exists(sourceDir) && IsSafeRuntimeFolder(sourceDir))
                DeleteSafeRuntimeFolder(sourceDir);
        }
        catch (Exception cleanupEx)
        {
            await RuntimeBuildJobService.AppendRecoveryLogAsync(logPath, $"Incomplete runtime source cleanup failed: {cleanupEx.Message}", MaxLogBytes());
        }
    }

    private async Task<(string Commit, string Path)> LatestLocalRuntimeVersionAsync(RuntimeBuildPreset preset)
    {
        var source = RuntimeSources()
            .Where(candidate => string.Equals(candidate.PresetId, preset.Id, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(candidate => candidate.DownloadedAt)
            .FirstOrDefault();
        if (source is not null)
            return (RuntimeBuildCatalogService.SourceCommit(source), source.SourceDir);

        var runtime = await LatestInstalledRuntimeAsync(preset);
        return runtime is null ? ("", "") : (RuntimeMetadataService.Commit(runtime), RuntimeMetadataService.Folder(runtime));
    }

    private async Task<RuntimeUpdateCheck> CheckRuntimeUpdateAsync(RuntimeBuildPreset preset)
    {
        var installed = await LatestInstalledRuntimeAsync(preset);
        if (installed is null)
            return new RuntimeUpdateCheck(false, true, "", await RemoteCommitAsync(preset));

        var localCommit = RuntimeMetadataService.Commit(installed);
        var remoteCommit = await RemoteCommitAsync(preset);
        return new RuntimeUpdateCheck(true, !RuntimeMetadataService.CommitsMatch(localCommit, remoteCommit), localCommit, remoteCommit);
    }

    private async Task<RuntimeRecord?> LatestInstalledRuntimeAsync(RuntimeBuildPreset preset)
    {
        if (_stateStore is null) return null;
        return (await _stateStore.ListRuntimesAsync())
            .Where(runtime => string.Equals(RuntimeMetadataService.ManagedPresetId(runtime), preset.Id, StringComparison.OrdinalIgnoreCase))
            .GroupBy(runtime => RuntimeMetadataService.Folder(runtime), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(runtime => runtime.UpdatedAt).First())
            .OrderByDescending(runtime => runtime.UpdatedAt)
            .FirstOrDefault();
    }

    private async Task<string> RemoteCommitAsync(RuntimeBuildPreset preset)
    {
        foreach (var remoteRef in RuntimeBuildCatalogService.RemoteRefs(preset))
        {
            var commit = await RunGitLsRemoteAsync(preset.RepoUrl, remoteRef);
            if (!string.IsNullOrWhiteSpace(commit)) return commit;
        }

        throw new InvalidOperationException($"Could not check remote updates for {preset.Label}.");
    }

    private async Task<string> RunGitLsRemoteAsync(string repoUrl, string remoteRef)
    {
        var psi = new ProcessStartInfo(HostExecutableResolver.GitExe())
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        psi.ArgumentList.Add("ls-remote");
        psi.ArgumentList.Add(repoUrl);
        psi.ArgumentList.Add(remoteRef);
        var result = await _processRunner.RunAsync(psi, TimeSpan.FromMinutes(5));
        if (result.ExitCode != 0) throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error);
        return RuntimeBuildCatalogService.FirstLsRemoteCommit(result.Output);
    }

    private async Task RunRuntimeBuildToolAsync(string script, string sourceDir, string buildDir, string installDir, RuntimeBuildPreset preset, string logPath, bool noUpdate, string processMarker, CancellationToken cancellationToken)
    {
        var mode = RuntimeBuildCatalogService.BuildMode(preset);
        var psi = RuntimeBuildToolService.CreateBuildProcessStartInfo(
            HostExecutableResolver.WindowsPowerShellExe(),
            script,
            sourceDir,
            buildDir,
            installDir,
            preset,
            mode,
            _settings.WslDistro,
            processMarker,
            mode == RuntimeMode.Wsl ? HostExecutableResolver.WslExe() : "",
            mode == RuntimeMode.Native ? HostExecutableResolver.GitExe() : "",
            mode == RuntimeMode.Native ? HostExecutableResolver.CMakeExe() : "",
            noUpdate);

        if (mode == RuntimeMode.Wsl)
            RegisterWslBuildMarker(processMarker);
        try
        {
            var result = await _processRunner.RunAsync(psi, TimeSpan.FromHours(6), cancellationToken);
            await BoundedLogFile.AppendAsync(logPath, result.Output + Environment.NewLine + result.Error, MaxLogBytes());
            if (result.ExitCode != 0) throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error);
        }
        catch
        {
            if (mode == RuntimeMode.Wsl)
                await CleanupWslBuildMarkerAsync(_settings.WslDistro, processMarker);
            throw;
        }
        finally
        {
            if (mode == RuntimeMode.Wsl)
                UnregisterWslBuildMarker(processMarker);
        }
    }

    private async Task DeleteRuntimeBuildAsync(RuntimeRecord runtime)
    {
        if (_stateStore is null) return;
        if (IsRuntimeActivelyUsed(runtime))
        {
            SetStatus("Unload the model before deleting the runtime it is using.");
            return;
        }
        var modelsByRuntime = await ModelsByRuntimeAsync();
        if (modelsByRuntime.TryGetValue(runtime.Id, out var modelNames) && modelNames.Count > 0)
        {
            SetStatus($"Update saved launch settings before deleting this runtime. Used by: {string.Join(", ", modelNames)}.");
            return;
        }

        if (!CanDeleteRuntimeFiles(runtime, out var folder, out var reason))
        {
            if (ThemedMessageBox.Show(
                    this,
                    $"Delete this runtime registration?\n\n{runtime.Name}\n{runtime.ExecutablePath}\n\n{reason}",
                    "Delete runtime",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            await _stateStore.DeleteRuntimeAsync(runtime.Id);
            await RefreshRuntimesAsync();
            await RefreshOverviewAsync();
            SetStatus($"Deleted runtime registration for {runtime.Name}. Runtime files were not deleted.");
            return;
        }

        if (ThemedMessageBox.Show(this, $"Delete runtime files and remove this runtime?\n\n{runtime.Name}\n{folder}", "Delete runtime", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await RunAsync("Deleting runtime build...", async () =>
        {
            await _stateStore.DeleteRuntimeAsync(runtime.Id);
            DeleteRuntimeFiles(folder);
            await RefreshRuntimesAsync();
            await RefreshOverviewAsync();
        });
    }

    private async Task<string> EnsureBuildToolScriptAsync()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(".Build-LlamaCppRuntime.ps1", StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(resourceName))
            throw new InvalidOperationException("Build tool resource was not packaged.");

        var toolsDir = Path.Combine(_workspaceRoot, "tools");
        Directory.CreateDirectory(toolsDir);
        var scriptPath = Path.Combine(toolsDir, "Build-LlamaCppRuntime.ps1");
        var tempPath = scriptPath + ".tmp";
        await using (var resource = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("Build tool resource could not be opened."))
        await using (var output = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await resource.CopyToAsync(output);
        }

        File.Move(tempPath, scriptPath, overwrite: true);
        return scriptPath;
    }
}
