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
        await MarkLoadedSessionsIfReadyAsync();
        UpdateFolderText(_modelsFolderText, _settings.ModelsRoot);
        UpdateFolderText(_runtimesFolderText, _settings.RuntimeRoot);
        _viewModel.Overview.ReplaceSessions(_sessions.Snapshots());
        _loadedSessionsGrid?.Items.Refresh();
    }

    private async Task RefreshRuntimeMetricsAsync()
    {
        if (!_sessions.HasRunningSessions && _runtimeDashboardModel is null && _runtimeMetricsGrid is null && _overviewRuntimeLogBox is null) return;
        if (_runtimeDashboardRefreshInFlight) return;

        _runtimeDashboardRefreshInFlight = true;
        try
        {
            var renderOverview = _viewModel.CurrentPage == "Overview";
            await MarkLoadedSessionsIfReadyAsync();
            var selectedOverviewModel = SelectedOverviewModel();
            if (selectedOverviewModel is not null && !IsModelActive(selectedOverviewModel) && IsModelLoaded(selectedOverviewModel))
            {
                _sessions.SelectModel(selectedOverviewModel.Id);
                _activeRuntimeSettings = _sessions.ActiveSettings;
            }

            var pollResults = await PollRuntimeMetricsForSessionsAsync(_sessions.Snapshots()
                .Where(session => session is { IsRunning: true, Status: LoadedModelSessionStatus.Running or LoadedModelSessionStatus.Warm })
                .ToArray());
            await TrackLifetimeTokenDeltasAsync(pollResults);
            await ApplyIdleUnloadPoliciesAsync(pollResults);

            if (selectedOverviewModel is not null && _sessions.SessionForModel(selectedOverviewModel.Id) is not { IsRunning: true })
            {
                ResetMetricCounters();
                if (renderOverview)
                {
                    SetMetricText(_runtimeDashboardModel, $"Stopped: {selectedOverviewModel.Name}");
                    SetMetricText(_runtimeDashboardRuntime, "No loaded runtime");
                    SetRuntimeModelProgress(LlamaRuntimeState.Stopped);
                    SetMetricText(_runtimeDashboardGpu, await CachedGpuSummaryAsync());
                    if (_overviewRuntimeLogBox is not null)
                        _overviewRuntimeLogBox.Text = "No runtime is loaded for the selected model.";
                    PopulateRuntimeMetricRows([]);
                    SetRuntimeMetricSummary("No runtime", "0", "Context No runtime\nKV cache No runtime");
                }
                return;
            }

            var selectedSession = selectedOverviewModel is null
                ? _sessions.SelectedSnapshot()
                : _sessions.SessionForModel(selectedOverviewModel.Id);
            var metricsSettings = selectedSession?.LaunchSettings ?? _sessions.ActiveSettings ?? _activeRuntimeSettings ?? _settings;
            var runtimeKey = selectedSession is null ? CurrentRuntimeMetricKey(metricsSettings) : RuntimeMetricKey(selectedSession);
            var selectedPollResult = selectedSession is null
                ? null
                : pollResults.FirstOrDefault(result => string.Equals(result.RuntimeKey, runtimeKey, StringComparison.Ordinal));
            var (modelName, runtimeName) = await ActiveRuntimeLabelsAsync();
            if (renderOverview)
            {
                RefreshModelStatusMetric(modelName);
                SetMetricText(_runtimeDashboardRuntime, runtimeName, emphasizeLoadedStatus: _llama.IsRunning);
            }
            if (_llama.State == LlamaRuntimeState.Failed)
                await SaveActiveRuntimeSessionsAsync();
            if (renderOverview)
            {
                UpdateRuntimeModelProgress();
                SetMetricText(_runtimeDashboardGpu, await CachedGpuSummaryAsync());
            }

            if (selectedSession is not { IsRunning: true })
            {
                ResetMetricCounters();
                if (renderOverview)
                {
                    RefreshRuntimeLogTail();
                    PopulateRuntimeMetricRows([]);
                    SetRuntimeMetricSummary("No runtime", "0", "Context No runtime\nKV cache No runtime");
                }
                return;
            }

            var slotSnapshot = selectedPollResult?.SlotSnapshot;
            if (renderOverview) RefreshRuntimeLogTail(slotSnapshot);
            if (!metricsSettings.EnableMetrics)
            {
                ResetMetricCounters();
                if (renderOverview) PopulateRuntimeMetricRows([]);
                await ApplyRuntimeMetricSummaryAsync([], metricsSettings, slotSnapshot, runtimeKey);
                return;
            }

            if (selectedPollResult is { Samples.Count: > 0 })
            {
                if (renderOverview) PopulateRuntimeMetricRows(selectedPollResult.Samples);
                await ApplyRuntimeMetricSummaryAsync(selectedPollResult.Samples, metricsSettings, slotSnapshot, runtimeKey);
            }
            else
            {
                if (renderOverview)
                    PopulateRuntimeMetricRowsOrLastKnown(selectedPollResult?.Error ?? "No metrics response.", runtimeKey);
                await ApplyRuntimeMetricSummaryAsync([], metricsSettings, slotSnapshot, runtimeKey);
            }
        }
        finally
        {
            _runtimeDashboardRefreshInFlight = false;
            UpdateOverviewModelActions();
            UpdateModelActionButtons();
        }
    }

    private async Task<IReadOnlyList<RuntimeMetricPollResult>> PollRuntimeMetricsForSessionsAsync(IReadOnlyList<LoadedModelSessionSnapshot> sessions)
    {
        if (sessions.Count == 0)
        {
            _lifetimeTokenCounters.RetainRuntimeKeys([]);
            return [];
        }

        return await Task.WhenAll(sessions.Select(PollRuntimeMetricsForSessionAsync));
    }

    private async Task<RuntimeMetricPollResult> PollRuntimeMetricsForSessionAsync(LoadedModelSessionSnapshot session)
    {
        var runtimeKey = RuntimeMetricKey(session);
        var settings = session.LaunchSettings;
        var slotTask = RuntimeSlotSnapshotAsync(settings);
        if (!settings.EnableMetrics)
            return new RuntimeMetricPollResult(session, runtimeKey, [], await slotTask, "");

        try
        {
            var uri = new Uri($"{RuntimeEndpointService.LocalServerBaseUrl(settings)}/metrics");
            var raw = await RuntimeEndpointService.RuntimeGetStringAsync(_metricsClient, uri.ToString(), settings);
            return new RuntimeMetricPollResult(session, runtimeKey, RuntimeMetrics.ParsePrometheus(raw), await slotTask, "");
        }
        catch (Exception ex)
        {
            return new RuntimeMetricPollResult(session, runtimeKey, [], await slotTask, ex.Message);
        }
    }

}
