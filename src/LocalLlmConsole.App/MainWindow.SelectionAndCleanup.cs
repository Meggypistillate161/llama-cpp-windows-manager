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
    private ModelRecord? SelectedModel() => _modelsGrid?.SelectedItem is ModelGridRow row ? row.Model : null;
    private RuntimeRecord? SelectedRuntime() => _runtimeGrid?.SelectedItem is RuntimeCatalogRow row ? row.Runtime : null;

    private static ModelRecord? ModelFromRow(ModelGridRow row) => row.Model;

    private async Task<ModelRecord?> FindModelByIdAsync(string modelId)
    {
        if (_stateStore is null || string.IsNullOrWhiteSpace(modelId)) return null;
        var models = await _stateStore.ListModelsAsync();
        return models.FirstOrDefault(model => string.Equals(model.Id, modelId, StringComparison.OrdinalIgnoreCase));
    }

    private static RuntimeRecord? RuntimeFromRow(RuntimeCatalogRow row) => row.Runtime;

    private static RuntimeSourceEntry? RuntimeSourceFromRow(RuntimeCatalogRow row) => row.Source;

    private static ModelRecord? ModelFromRowButton(object sender)
    {
        if ((sender as FrameworkElement)?.Tag is not ModelGridRow row) return null;
        return ModelFromRow(row);
    }

    private RuntimeRecord? RuntimeFromRowButton(object sender)
    {
        if ((sender as FrameworkElement)?.Tag is not RuntimeCatalogRow row) return null;
        return RuntimeFromRow(row);
    }

    private RuntimeSourceEntry? RuntimeSourceFromRowButton(object sender)
    {
        if ((sender as FrameworkElement)?.Tag is not RuntimeCatalogRow row) return null;
        return RuntimeSourceFromRow(row);
    }

    private RuntimeBuildPreset? RuntimeBuildPresetFromRowButton(object sender)
    {
        if ((sender as FrameworkElement)?.Tag is not RuntimeBuildPresetRow row) return null;
        return row.Preset;
    }

    private RuntimePackagePreset? RuntimePackagePresetFromRowButton(object sender)
    {
        if ((sender as FrameworkElement)?.Tag is not RuntimePackagePresetRow row) return null;
        return row.Preset;
    }

    private bool IsRuntimeActivelyUsed(RuntimeRecord runtime)
        => _sessions.Snapshots().Any(session => session.IsRunning && string.Equals(session.RuntimeId, runtime.Id, StringComparison.OrdinalIgnoreCase));

    private bool CanDeleteRuntimeFiles(RuntimeRecord runtime, out string folder, out string reason)
        => RuntimeFileService.CanDeleteRuntimeFiles(runtime, _settings.RuntimeRoot, out folder, out reason);

    private bool IsSafeRuntimeFolder(string folder)
        => RuntimeFileService.IsSafeRuntimeFolder(_settings.RuntimeRoot, folder);

    private void DeleteSafeRuntimeFolder(string folder)
        => RuntimeFileService.DeleteSafeRuntimeFolder(_settings.RuntimeRoot, folder);

    private void DeleteRuntimeFiles(string folder)
        => RuntimeFileService.DeleteRuntimeFiles(_settings.RuntimeRoot, folder);

    private JobRecord? JobFromRowButton(object sender)
    {
        if ((sender as FrameworkElement)?.Tag is not UiRow row) return null;
        return JobFromRow(row);
    }

    private static JobRecord? JobFromRow(UiRow? row)
    {
        if (row is null) return null;
        try { return row.Data.Deserialize<JobRecord>(); }
        catch { return null; }
    }

    private JobRecord? SelectedDownloadJob()
    {
        if (_downloadHistoryGrid?.SelectedItem is not UiRow row) return null;
        return JobFromRow(row);
    }

    private async Task<HuggingFaceInstallInventory> InstalledHuggingFaceInventoryAsync()
    {
        if (_stateStore is null) return HuggingFaceInstallStateService.BuildInventory([]);
        return HuggingFaceInstallStateService.BuildInventory(await _stateStore.ListModelsAsync());
    }

    private async Task RefreshHuggingFaceInstallStateAsync()
    {
        if (_hfShowingDownloadHistory || _hfGrid is null || _viewModel.HuggingFace.SearchRows.Count == 0) return;
        var installed = await InstalledHuggingFaceInventoryAsync();
        foreach (var row in _viewModel.HuggingFace.SearchRows)
        {
            var file = row.Data.Deserialize<HuggingFaceFile>();
            if (file is null) continue;
            var isInstalled = HuggingFaceInstallStateService.IsInstalled(file, installed, _settings.ModelsRoot);
            row.C6 = isInstalled ? "Installed" : "Download";
            row.B1 = !isInstalled;
        }
        _hfGrid.Items.Refresh();
    }

    private void RegisterWslBuildMarker(string marker)
    {
        lock (_wslBuildMarkerGate)
            _activeWslBuildMarkers.Add(marker);
    }

    private void UnregisterWslBuildMarker(string marker)
    {
        lock (_wslBuildMarkerGate)
            _activeWslBuildMarkers.Remove(marker);
    }

    private async Task CleanupActiveWslBuildsAsync()
    {
        string[] markers;
        lock (_wslBuildMarkerGate)
            markers = _activeWslBuildMarkers.ToArray();

        foreach (var marker in markers)
            await CleanupWslBuildMarkerAsync(_settings.WslDistro, marker);
    }

    private async Task CleanupInterruptedRuntimeBuildJobsAsync()
    {
        if (_stateStore is null) return;
        foreach (var job in await _stateStore.ListJobsAsync())
        {
            if (!string.Equals(job.Kind, "runtime-build", StringComparison.OrdinalIgnoreCase)
                || job.Status != JobStatus.Interrupted)
                continue;

            try
            {
                var payload = JsonNode.Parse(job.PayloadJson);
                var marker = payload?["processMarker"]?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(marker)) continue;
                var distro = payload?["wslDistro"]?.ToString()
                    ?? payload?["distro"]?.ToString()
                    ?? _settings.WslDistro;
                await CleanupWslBuildMarkerAsync(distro, marker);
            }
            catch
            {
                // Stale job payloads are best-effort recovery only.
            }
        }
    }

    private async Task CleanupWslBuildMarkerAsync(string distro, string marker)
    {
        if (string.IsNullOrWhiteSpace(distro) || string.IsNullOrWhiteSpace(marker)) return;
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
            foreach (var arg in new[] { "-d", distro, "--", "bash", "-lc", CommandLineService.WslKillByEnvironmentMarkerCommand(marker) })
                psi.ArgumentList.Add(arg);
            _ = await _processRunner.RunAsync(psi, TimeSpan.FromSeconds(10));
        }
        catch
        {
            // Best effort cleanup for cancelled WSL build jobs.
        }
    }
}
