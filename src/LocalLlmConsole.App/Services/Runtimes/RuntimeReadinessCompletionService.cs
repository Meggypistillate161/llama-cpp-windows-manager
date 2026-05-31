namespace LocalLlmConsole.Services;

public sealed record RuntimeReadinessCompletionRequest(
    RuntimeReadinessStatus Status,
    string ModelName,
    AppSettings LaunchSettings,
    bool ModelIsStillLoading,
    bool IsOverviewPage);

public sealed record RuntimeReadinessCompletionPlan(
    bool StopLoadingStatus,
    bool ShowLoadedDuration,
    bool SelectLoadedOverviewModel,
    bool SaveActiveRuntimeSessions,
    bool UpdateRuntimeProgress,
    bool UpdateActionButtons,
    bool RefreshRuntimeMetrics,
    string StatusMessage);

public sealed class RuntimeReadinessCompletionService
{
    public RuntimeReadinessCompletionPlan Build(RuntimeReadinessCompletionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Status switch
        {
            RuntimeReadinessStatus.NoLongerLoading => new RuntimeReadinessCompletionPlan(
                StopLoadingStatus: request.ModelIsStillLoading,
                ShowLoadedDuration: false,
                SelectLoadedOverviewModel: false,
                SaveActiveRuntimeSessions: false,
                UpdateRuntimeProgress: false,
                UpdateActionButtons: false,
                RefreshRuntimeMetrics: false,
                StatusMessage: ""),
            RuntimeReadinessStatus.Loaded => new RuntimeReadinessCompletionPlan(
                StopLoadingStatus: request.ModelIsStillLoading,
                ShowLoadedDuration: request.ModelIsStillLoading,
                SelectLoadedOverviewModel: true,
                SaveActiveRuntimeSessions: true,
                UpdateRuntimeProgress: true,
                UpdateActionButtons: true,
                RefreshRuntimeMetrics: request.IsOverviewPage,
                StatusMessage: $"Loaded {request.ModelName} at {RuntimeEndpointService.EndpointDisplay(request.LaunchSettings)}."),
            _ => new RuntimeReadinessCompletionPlan(
                StopLoadingStatus: false,
                ShowLoadedDuration: false,
                SelectLoadedOverviewModel: false,
                SaveActiveRuntimeSessions: false,
                UpdateRuntimeProgress: false,
                UpdateActionButtons: false,
                RefreshRuntimeMetrics: false,
                StatusMessage: "")
        };
    }
}
