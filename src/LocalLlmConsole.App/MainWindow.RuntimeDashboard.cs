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
    private async Task RefreshJobsAsync()
    {
        if (_stateStore is null) return;
        var selectedRuntimeJobId = JobFromRow(_runtimeJobsGrid?.SelectedItem as UiRow)?.Id;
        _viewModel.Jobs.ReplaceJobs(await _stateStore.ListJobsAsync());

        if (_runtimeJobsGrid is not null)
        {
            _runtimeJobsGrid.SelectedItem = string.IsNullOrWhiteSpace(selectedRuntimeJobId)
                ? _viewModel.Jobs.RuntimeRows.FirstOrDefault()
                : _viewModel.Jobs.RuntimeRows.FirstOrDefault(row =>
                    string.Equals(row.Data["Id"]?.ToString(), selectedRuntimeJobId, StringComparison.OrdinalIgnoreCase))
                  ?? _viewModel.Jobs.RuntimeRows.FirstOrDefault();
            _runtimeJobsGrid.Items.Refresh();
        }
    }

    private async Task RefreshOverviewAsync()
    {
        await Task.CompletedTask;
        UpdateFolderText(_modelsFolderText, _settings.ModelsRoot);
        UpdateFolderText(_runtimesFolderText, _settings.RuntimeRoot);
    }

    private async Task RefreshRuntimeMetricsAsync()
    {
        if (_runtimeDashboardModel is null && _runtimeMetricsGrid is null && _overviewRuntimeLogBox is null) return;
        if (_runtimeDashboardRefreshInFlight) return;

        _runtimeDashboardRefreshInFlight = true;
        try
        {
            var renderOverview = _viewModel.CurrentPage == "Overview";
            var metricsSettings = _activeRuntimeSettings ?? _settings;
            await MarkRuntimeLoadedIfReadyAsync(metricsSettings);
            var (modelName, runtimeName) = await ActiveRuntimeLabelsAsync();
            if (renderOverview)
            {
                RefreshModelStatusMetric(modelName);
                SetMetricText(_runtimeDashboardRuntime, runtimeName, emphasizeLoadedStatus: _llama.IsRunning);
            }
            if (_llama.State == LlamaRuntimeState.Failed)
                ClearActiveRuntimeSession();
            if (renderOverview)
            {
                UpdateRuntimeModelProgress();
                SetMetricText(_runtimeDashboardGpu, await CachedGpuSummaryAsync());
            }

            if (!_llama.IsRunning)
            {
                ResetMetricCounters();
                ResetLifetimeCounters();
                ResetIdleCounters();
                if (renderOverview)
                {
                    RefreshRuntimeLogTail();
                    PopulateRuntimeMetricRows([]);
                    SetRuntimeMetricSummary("No runtime", "0", "Context No runtime\nKV cache No runtime");
                }
                return;
            }

            var slotSnapshot = await RuntimeSlotSnapshotAsync(metricsSettings);
            if (renderOverview) RefreshRuntimeLogTail(slotSnapshot);
            if (!metricsSettings.EnableMetrics)
            {
                ResetMetricCounters();
                if (renderOverview) PopulateRuntimeMetricRows([]);
                await ApplyRuntimeMetricSummaryAsync([], metricsSettings, slotSnapshot);
                return;
            }

            try
            {
                var uri = new Uri($"{RuntimeEndpointService.LocalServerBaseUrl(metricsSettings)}/metrics");
                var raw = await RuntimeEndpointService.RuntimeGetStringAsync(_metricsClient, uri.ToString(), metricsSettings);
                var samples = RuntimeMetrics.ParsePrometheus(raw);
                if (renderOverview) PopulateRuntimeMetricRows(samples);
                await ApplyRuntimeMetricSummaryAsync(samples, metricsSettings, slotSnapshot);
            }
            catch (Exception ex)
            {
                if (renderOverview)
                    PopulateRuntimeMetricRowsOrLastKnown(ex.Message);
                await ApplyRuntimeMetricSummaryAsync([], metricsSettings, slotSnapshot);
            }
        }
        finally
        {
            _runtimeDashboardRefreshInFlight = false;
            UpdateOverviewModelActions();
            UpdateModelActionButtons();
        }
    }

}
