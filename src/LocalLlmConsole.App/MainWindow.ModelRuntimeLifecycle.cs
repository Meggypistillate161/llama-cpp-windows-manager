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
    private async Task StartModelRuntimeAsync(RuntimeRecord runtime, ModelRecord model, AppSettings launchSettings)
    {
        StartModelLoadingTimer(model.Name, launchSettings);
        try
        {
            launchSettings = await EnsureModelApiKeyAsync(launchSettings);
            await EnsureRuntimeLaunchPrerequisitesAsync(runtime, launchSettings);
            await _llama.StartAsync(runtime, model, launchSettings, Path.Combine(_workspaceRoot, "logs"));
            _activeRuntimeSettings = launchSettings;
            await SaveActiveRuntimeSessionAsync(runtime, model, launchSettings);
            StartRuntimeReadinessMonitor(model, launchSettings);
            StartRuntimeDashboardRefreshTimer();
            UpdateModelLoadingStatus();
            await RefreshOverviewAsync();
            await RefreshOverviewModelSelectorAsync();
            await Task.Delay(750);
            await RefreshRuntimeMetricsAsync();
            if (_viewModel.CurrentPage != "Overview") StopRuntimeDashboardRefreshTimer();
            UpdateModelActionButtons();
            UpdateOverviewModelActions();
            if (_llama.State == LlamaRuntimeState.Failed)
            {
                StopModelLoadingTimer();
                ClearActiveRuntimeSession();
                SetStatus($"Failed to load {model.Name}. Check the runtime log.");
            }
            else
            {
                UpdateModelLoadingStatus();
            }
        }
        catch
        {
            StopModelLoadingTimer();
            throw;
        }
    }

    private void StartModelLoadingTimer(string modelName, AppSettings launchSettings)
    {
        StopModelLoadingTimer();
        _modelLoadingModelName = modelName;
        _modelLoadingEndpoint = RuntimeEndpointService.EndpointDisplay(launchSettings);
        _modelLoadingStartedAt = DateTimeOffset.Now;
        _modelLoadingTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _modelLoadingTimer.Tick += (_, _) => UpdateModelLoadingStatus();
        UpdateModelLoadingStatus();
        _modelLoadingTimer.Start();
    }

    private void UpdateModelLoadingStatus()
    {
        if (string.IsNullOrWhiteSpace(_modelLoadingModelName)) return;
        var elapsed = DisplayFormatService.Elapsed(DateTimeOffset.Now - _modelLoadingStartedAt);
        SetMetricText(_runtimeDashboardModel, $"Loading {_modelLoadingModelName} ({elapsed})");
        UpdateRuntimeModelProgress();
        SetStatus($"Loading {_modelLoadingModelName} at {_modelLoadingEndpoint}.");
    }

    private void RefreshModelStatusMetric(string fallbackModelStatus)
    {
        if (_modelLoadingTimer is not null)
        {
            UpdateModelLoadingStatus();
            return;
        }

        if (_modelLoadedStatusTimer is not null && !string.IsNullOrWhiteSpace(_modelLoadedStatusText))
        {
            SetMetricText(_runtimeDashboardModel, _modelLoadedStatusText);
            return;
        }

        SetMetricText(_runtimeDashboardModel, fallbackModelStatus);
    }

    private void StopModelLoadingTimer(bool showLoadedDuration = false, string loadedModelName = "")
    {
        StopModelLoadedStatusTimer();
        var hadLoadingStatus = _modelLoadingTimer is not null || !string.IsNullOrWhiteSpace(_modelLoadingModelName);
        var elapsed = hadLoadingStatus ? DateTimeOffset.Now - _modelLoadingStartedAt : TimeSpan.Zero;
        var modelName = string.IsNullOrWhiteSpace(loadedModelName) ? _modelLoadingModelName : loadedModelName;
        _modelLoadingTimer?.Stop();
        _modelLoadingTimer = null;
        _modelLoadingModelName = "";
        _modelLoadingEndpoint = "";

        if (showLoadedDuration && hadLoadingStatus && !string.IsNullOrWhiteSpace(modelName))
            ShowModelLoadedStatus(modelName, elapsed);
    }

    private void ShowModelLoadedStatus(string modelName, TimeSpan elapsed)
    {
        _modelLoadedStatusText = $"Loaded: {modelName} in {DisplayFormatService.Elapsed(elapsed)}";
        SetMetricText(_runtimeDashboardModel, _modelLoadedStatusText);
        UpdateRuntimeModelProgress();

        _modelLoadedStatusTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(4)
        };
        _modelLoadedStatusTimer.Tick += async (_, _) =>
        {
            StopModelLoadedStatusTimer();
            await RefreshRuntimeMetricsAsync();
        };
        _modelLoadedStatusTimer.Start();
    }

    private void StopModelLoadedStatusTimer()
    {
        if (_modelLoadedStatusTimer is null) return;
        _modelLoadedStatusTimer.Stop();
        _modelLoadedStatusTimer = null;
        _modelLoadedStatusText = "";
    }

    private void StartRuntimeReadinessMonitor(ModelRecord model, AppSettings launchSettings)
    {
        StopRuntimeReadinessMonitor();
        _runtimeReadinessCts = new CancellationTokenSource();
        RunBackground(() => MonitorRuntimeReadinessAsync(model.Id, model.Name, launchSettings, _runtimeReadinessCts.Token), "Runtime readiness monitor failed");
    }

    private void StopRuntimeReadinessMonitor()
    {
        if (_runtimeReadinessCts is null) return;
        try { _runtimeReadinessCts.Cancel(); } catch {}
        _runtimeReadinessCts.Dispose();
        _runtimeReadinessCts = null;
    }

    private async Task MonitorRuntimeReadinessAsync(string modelId, string modelName, AppSettings launchSettings, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                if (!_llama.IsRunning
                    || _llama.State != LlamaRuntimeState.Loading
                    || !string.Equals(_llama.ActiveModelId, modelId, StringComparison.OrdinalIgnoreCase))
                {
                    StopModelLoadingTimer();
                    return;
                }

                if (!await RuntimeEndpointAliveAsync(launchSettings)) continue;
                if (!_llama.MarkLoadedIfRunning()) return;

                StopModelLoadingTimer(showLoadedDuration: true, loadedModelName: modelName);
                SetStatus($"Loaded {modelName} at {RuntimeEndpointService.EndpointDisplay(launchSettings)}.");
                UpdateRuntimeModelProgress();
                UpdateModelActionButtons();
                UpdateOverviewModelActions();
                if (_viewModel.CurrentPage == "Overview") await RefreshRuntimeMetricsAsync();
                return;
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task StopLoadedRuntimeAsync()
    {
        StopRuntimeReadinessMonitor();
        StopModelLoadingTimer();
        ResetLifetimeCounters();
        ResetIdleCounters();
        _llama.Stop();
        _activeRuntimeSettings = null;
        ClearActiveRuntimeSession();
        ResetMetricCounters();
        await RefreshOverviewAsync();
        await RefreshRuntimeMetricsAsync();
        UpdateModelActionButtons();
        UpdateOverviewModelActions();
        SetStatus("Runtime stopped.");
    }
}
