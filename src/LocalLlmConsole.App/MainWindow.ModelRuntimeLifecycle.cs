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
        try
        {
            launchSettings = await EnsureModelApiKeyAsync(launchSettings);
            var sessionId = LoadedModelSessionManager.SessionIdFor(model.Id);
            if (_sessions.ReservedPorts(sessionId).Contains(launchSettings.Port))
                throw new InvalidOperationException($"Port {launchSettings.Port} is already assigned to another loaded model. Set a unique model port next to the runtime before launching.");
            await EnsureRuntimeLaunchPrerequisitesAsync(runtime, launchSettings);
            if (!await ConfirmVramAdmissionAsync(runtime, model, launchSettings)) return;
            StartModelLoadingTimer(model.Id, model.Name, launchSettings);
            var snapshot = await _sessions.StartAsync(runtime, model, launchSettings, Path.Combine(_workspaceRoot, "logs"));
            _activeRuntimeSettings = snapshot.LaunchSettings;
            await SaveActiveRuntimeSessionsAsync();
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
                await SaveActiveRuntimeSessionsAsync();
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

    private void StartModelLoadingTimer(string modelId, string modelName, AppSettings launchSettings)
    {
        StopModelLoadingTimer();
        _modelLoadingModelId = modelId;
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
        var selectedOverviewModel = SelectedOverviewModel();
        if (selectedOverviewModel is not null
            && !string.Equals(selectedOverviewModel.Id, _modelLoadingModelId, StringComparison.OrdinalIgnoreCase))
            return;
        var elapsed = DisplayFormatService.Elapsed(DateTimeOffset.Now - _modelLoadingStartedAt);
        SetMetricText(_runtimeDashboardModel, $"Loading {_modelLoadingModelName} ({elapsed})");
        UpdateRuntimeModelProgress();
        SetStatus($"Loading {_modelLoadingModelName} at {_modelLoadingEndpoint}.");
    }

    private void RefreshModelStatusMetric(string fallbackModelStatus)
    {
        var selectedOverviewModel = SelectedOverviewModel();
        var loadingSelectedModel = _modelLoadingTimer is not null
            && (selectedOverviewModel is null || string.Equals(selectedOverviewModel.Id, _modelLoadingModelId, StringComparison.OrdinalIgnoreCase));
        if (loadingSelectedModel)
        {
            UpdateModelLoadingStatus();
            return;
        }

        var loadedStatusSelectedModel = _modelLoadedStatusTimer is not null
            && !string.IsNullOrWhiteSpace(_modelLoadedStatusText)
            && (selectedOverviewModel is null || string.Equals(selectedOverviewModel.Id, _modelLoadedStatusModelId, StringComparison.OrdinalIgnoreCase));
        if (loadedStatusSelectedModel)
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
        var modelId = _modelLoadingModelId;
        var modelName = string.IsNullOrWhiteSpace(loadedModelName) ? _modelLoadingModelName : loadedModelName;
        _modelLoadingTimer?.Stop();
        _modelLoadingTimer = null;
        _modelLoadingModelId = "";
        _modelLoadingModelName = "";
        _modelLoadingEndpoint = "";

        if (showLoadedDuration && hadLoadingStatus && !string.IsNullOrWhiteSpace(modelName))
            ShowModelLoadedStatus(modelId, modelName, elapsed);
    }

    private void ShowModelLoadedStatus(string modelId, string modelName, TimeSpan elapsed)
    {
        _modelLoadedStatusModelId = modelId;
        _modelLoadedStatusText = $"Loaded: {modelName} in {DisplayFormatService.Elapsed(elapsed)}";
        var selectedOverviewModel = SelectedOverviewModel();
        if (selectedOverviewModel is null || string.Equals(selectedOverviewModel.Id, modelId, StringComparison.OrdinalIgnoreCase))
        {
            SetMetricText(_runtimeDashboardModel, _modelLoadedStatusText);
            UpdateRuntimeModelProgress();
        }

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
        _modelLoadedStatusModelId = "";
        _modelLoadedStatusText = "";
    }

    private void StartRuntimeReadinessMonitor(ModelRecord model, AppSettings launchSettings)
    {
        StopRuntimeReadinessMonitor(model.Id);
        var cts = new CancellationTokenSource();
        _runtimeReadinessMonitors[model.Id] = cts;
        RunBackground(() => MonitorRuntimeReadinessAsync(model.Id, model.Name, launchSettings, cts), "Runtime readiness monitor failed");
    }

    private void StopRuntimeReadinessMonitor()
    {
        foreach (var modelId in _runtimeReadinessMonitors.Keys.ToArray())
            StopRuntimeReadinessMonitor(modelId);
    }

    private void StopRuntimeReadinessMonitor(string modelId)
    {
        if (!_runtimeReadinessMonitors.Remove(modelId, out var cts)) return;
        try { cts.Cancel(); } catch { }
        cts.Dispose();
    }

    private async Task MonitorRuntimeReadinessAsync(string modelId, string modelName, AppSettings launchSettings, CancellationTokenSource readinessCts)
    {
        var cancellationToken = readinessCts.Token;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                var session = _sessions.SessionForModel(modelId);
                if (session is not { IsRunning: true, Status: LoadedModelSessionStatus.Loading })
                {
                    if (string.Equals(_modelLoadingModelId, modelId, StringComparison.OrdinalIgnoreCase))
                        StopModelLoadingTimer();
                    return;
                }

                if (!await RuntimeEndpointAliveAsync(launchSettings)) continue;
                if (!_sessions.MarkModelLoadedIfRunning(modelId)) return;

                if (string.Equals(_modelLoadingModelId, modelId, StringComparison.OrdinalIgnoreCase))
                    StopModelLoadingTimer(showLoadedDuration: true, loadedModelName: modelName);
                await SaveActiveRuntimeSessionsAsync();
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
        finally
        {
            if (_runtimeReadinessMonitors.TryGetValue(modelId, out var cts)
                && ReferenceEquals(cts, readinessCts))
                _runtimeReadinessMonitors.Remove(modelId);
            readinessCts.Dispose();
        }
    }

    private async Task StopLoadedRuntimeAsync()
    {
        var selectedSession = _sessions.SelectedSnapshot();
        var selectedModelId = selectedSession?.ModelId ?? "";
        var selectedModel = await FindModelByIdAsync(selectedModelId);
        if (!string.IsNullOrWhiteSpace(selectedModelId))
            StopRuntimeReadinessMonitor(selectedModelId);
        if (string.IsNullOrWhiteSpace(selectedModelId)
            || string.Equals(_modelLoadingModelId, selectedModelId, StringComparison.OrdinalIgnoreCase))
            StopModelLoadingTimer();
        ResetLifetimeCounters(selectedSession);
        ResetIdleCounters(selectedSession);
        await _sessions.StopSelectedAsync();
        _activeRuntimeSettings = _sessions.ActiveSettings;
        await SaveActiveRuntimeSessionsAsync();
        ResetMetricCounters();
        await RefreshOverviewAsync();
        await RefreshRuntimeMetricsAsync();
        UpdateModelActionButtons();
        UpdateOverviewModelActions();
        SetStatus("Runtime stopped.");
    }

    private async Task StopModelRuntimeAsync(ModelRecord model)
    {
        var wasSelected = IsModelActive(model);
        var stoppedSession = _sessions.SessionForModel(model.Id);
        StopRuntimeReadinessMonitor(model.Id);
        if (string.Equals(_modelLoadingModelId, model.Id, StringComparison.OrdinalIgnoreCase))
            StopModelLoadingTimer();
        if (wasSelected)
        {
            ResetMetricCounters();
        }
        ResetLifetimeCounters(stoppedSession);
        ResetIdleCounters(stoppedSession);

        await _sessions.StopModelAsync(model.Id);
        _activeRuntimeSettings = _sessions.ActiveSettings;
        await SaveActiveRuntimeSessionsAsync();
        await RefreshOverviewAsync();
        await RefreshRuntimeMetricsAsync();
        UpdateModelActionButtons();
        UpdateOverviewModelActions();
        SetStatus($"Unloaded {model.Name}.");
    }

    private async Task SwitchToLoadedModelAsync(ModelRecord model)
    {
        if (!_sessions.SelectModel(model.Id))
        {
            SetStatus($"{model.Name} is not loaded.");
            return;
        }

        _activeRuntimeSettings = _sessions.ActiveSettings;
        ResetMetricCounters();
        await SaveActiveRuntimeSessionsAsync();
        StartRuntimeDashboardRefreshTimer();
        await RefreshOverviewModelSelectorAsync();
        await RefreshRuntimeMetricsAsync();
        UpdateModelActionButtons();
        UpdateOverviewModelActions();
        SetStatus($"Selected loaded model {model.Name}.");
    }
}
