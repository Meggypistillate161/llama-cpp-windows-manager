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
        var selectedRuntimeJobId = _runtimesPage.SelectedRuntimeJobId();
        _viewModel.Jobs.ReplaceJobs(await AppServices.StateStore.ListJobsAsync());
        _runtimesPage.RestoreRuntimeJobSelection(selectedRuntimeJobId, _viewModel.Jobs.RuntimeRows);
    }

    private async Task RefreshOverviewAsync()
    {
        await MarkLoadedSessionsIfReadyAsync();
        UpdateFolderText(_modelsPage.ModelsFolderText, _settings.ModelsRoot);
        UpdateFolderText(_runtimesPage.RuntimesFolderText, _settings.RuntimeRoot);
        RefreshOverviewSessionRows();
    }

    private async Task RefreshRuntimeMetricsAsync()
    {
        await _coreServices.Runtime.RuntimeDashboardRefreshApplication.RefreshAsync(
            new RuntimeDashboardRefreshApplicationRequest(
                new RuntimeDashboardRefreshTarget(
                    _sessions.HasRunningSessions,
                    _runtimeDashboardPage.ModelMetric is not null,
                    _runtimeDashboardPage.RuntimeMetricsGrid is not null,
                    _runtimeDashboardPage.RuntimeLogBox is not null),
                _viewModel.CurrentPage == "Overview",
                _settings,
                _llama.ActiveModelId,
                _llama.ActiveRuntimeId,
                _llama.State,
                _llama.IsRunning),
            RuntimeDashboardRefreshActions());
    }

    private async Task RenderStoppedSelectedOverviewModelAsync(ModelRecord? selectedOverviewModel, bool renderOverview)
    {
        ResetMetricCounters();
        if (!renderOverview || selectedOverviewModel is null) return;

        SetMetricText(_runtimeDashboardPage.ModelMetric, $"Stopped: {selectedOverviewModel.Name}");
        SetMetricText(_runtimeDashboardPage.RuntimeMetric, "No loaded runtime");
        SetRuntimeModelProgress(LlamaRuntimeState.Stopped);
        SetMetricText(_runtimeDashboardPage.GpuMetric, await CachedGpuSummaryAsync());
        if (_runtimeDashboardPage.RuntimeLogBox is not null)
            _runtimeDashboardPage.RuntimeLogBox.Text = "No runtime is loaded for the selected model.";
        ApplyRuntimeMetricRows(new RuntimeMetricRowsRenderPlan([], null));
        ApplyRuntimeMetricSummary(RuntimeMetricSummaryPresentation.NoRuntime);
    }

    private RuntimeDashboardMetricsApplicationActions RuntimeDashboardMetricsActions()
        => new(
            RefreshRuntimeLogTail,
            ApplyRuntimeMetricRows,
            ApplyRuntimeMetricSummary);

    private RuntimeDashboardRefreshApplicationActions RuntimeDashboardRefreshActions()
        => new(
            MarkLoadedSessionsIfReadyAsync,
            RefreshOverviewSessionRows,
            () => _sessions.Snapshots(),
            TrackLifetimeTokenDeltasAsync,
            ApplyIdleUnloadPoliciesAsync,
            SelectedOverviewModel,
            IsModelActive,
            IsModelLoaded,
            _sessions.SessionForModel,
            _sessions.SelectedSnapshot,
            () => _sessions.ActiveSettings,
            () => _activeRuntimeSettings,
            modelId => _coreServices.Runtime.RuntimeSessions.SelectModel(modelId),
            settings => _activeRuntimeSettings = settings,
            ActiveRuntimeLabelsAsync,
            RefreshModelStatusMetric,
            (runtimeName, emphasizeLoadedStatus) => SetMetricText(_runtimeDashboardPage.RuntimeMetric, runtimeName, emphasizeLoadedStatus),
            SaveActiveRuntimeSessionsAsync,
            UpdateRuntimeModelProgress,
            CachedGpuSummaryAsync,
            summary => SetMetricText(_runtimeDashboardPage.GpuMetric, summary),
            RenderStoppedSelectedOverviewModelAsync,
            RuntimeDashboardMetricsActions(),
            UpdateOverviewModelActions);
}
