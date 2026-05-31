namespace LocalLlmConsole.Services;

public sealed record RuntimeStopApplicationRequest(
    RuntimeStopDecision Decision,
    LoadedModelSessionSnapshot? StoppedSession,
    bool ResetMetricCountersBeforeStop,
    Func<Task<RuntimeSessionStopResult>> StopAsync);

public sealed record RuntimeStopApplicationActions(
    Action<string> StopReadinessMonitor,
    Action StopLoadingTimer,
    Action ResetMetricCounters,
    Action<LoadedModelSessionSnapshot?> ResetLifetimeCounters,
    Action<LoadedModelSessionSnapshot?> ResetIdleCounters,
    Action<AppSettings?> SetActiveRuntimeSettings,
    Func<Task> SaveActiveRuntimeSessionsAsync,
    Func<Task> RefreshOverviewAsync,
    Func<Task> RefreshRuntimeMetricsAsync,
    Action UpdateActionButtons,
    Action<string> SetStatus);

public sealed record RuntimeSwitchApplicationActions(
    Action<AppSettings?> SetActiveRuntimeSettings,
    Action ResetMetricCounters,
    Func<Task> SaveActiveRuntimeSessionsAsync,
    Action StartRuntimeDashboardRefresh,
    Func<Task> RefreshOverviewModelSelectorAsync,
    Func<Task> RefreshRuntimeMetricsAsync,
    Action UpdateActionButtons,
    Action<string> SetStatus);

public sealed class RuntimeSessionFollowupApplicationService
{
    public async Task ApplyStopAsync(
        RuntimeStopApplicationRequest request,
        RuntimeStopApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        var decision = request.Decision;
        if (!string.IsNullOrWhiteSpace(decision.ReadinessMonitorModelId))
            actions.StopReadinessMonitor(decision.ReadinessMonitorModelId);
        if (decision.StopLoadingStatus)
            actions.StopLoadingTimer();
        if (request.ResetMetricCountersBeforeStop && decision.ResetMetricCounters)
            actions.ResetMetricCounters();

        actions.ResetLifetimeCounters(request.StoppedSession);
        actions.ResetIdleCounters(request.StoppedSession);

        var result = await request.StopAsync();
        actions.SetActiveRuntimeSettings(result.ActiveSettings);
        await actions.SaveActiveRuntimeSessionsAsync();

        if (!request.ResetMetricCountersBeforeStop && decision.ResetMetricCounters)
            actions.ResetMetricCounters();

        await actions.RefreshOverviewAsync();
        await actions.RefreshRuntimeMetricsAsync();
        actions.UpdateActionButtons();
        actions.SetStatus(decision.StatusMessage);
    }

    public async Task ApplySwitchAsync(
        RuntimeSwitchCommandResult result,
        RuntimeSwitchApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(actions);

        var decision = result.Decision;
        if (!decision.Selected)
        {
            actions.SetStatus(decision.StatusMessage);
            return;
        }

        actions.SetActiveRuntimeSettings(result.ActiveSettings);
        if (decision.ResetMetricCounters)
            actions.ResetMetricCounters();
        await actions.SaveActiveRuntimeSessionsAsync();
        if (decision.StartDashboardRefresh)
            actions.StartRuntimeDashboardRefresh();
        await actions.RefreshOverviewModelSelectorAsync();
        await actions.RefreshRuntimeMetricsAsync();
        actions.UpdateActionButtons();
        actions.SetStatus(decision.StatusMessage);
    }
}
