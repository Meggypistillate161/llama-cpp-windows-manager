namespace LocalLlmConsole.Services;

public sealed record ModelRuntimeStatusRenderPlan(
    bool ShouldRender,
    string MetricText,
    bool UpdateProgress,
    string StatusText)
{
    public static ModelRuntimeStatusRenderPlan None { get; } = new(false, "", false, "");
}

public sealed class ModelRuntimeStatusRenderService
{
    public ModelRuntimeStatusRenderPlan LoadingTick(ModelRuntimeStatusDisplay? status)
        => status is null
            ? ModelRuntimeStatusRenderPlan.None
            : Render(status, updateProgress: true, includeStatusText: true);

    public ModelRuntimeStatusRenderPlan DashboardRefresh(
        ModelRuntimeStatusDisplay status,
        bool hasLoadedStatusTimer)
    {
        ArgumentNullException.ThrowIfNull(status);

        return status.Kind switch
        {
            ModelRuntimeStatusKind.Loading => Render(status, updateProgress: true, includeStatusText: true),
            ModelRuntimeStatusKind.Loaded when hasLoadedStatusTimer => Render(status, updateProgress: false, includeStatusText: false),
            _ => Render(status, updateProgress: false, includeStatusText: false)
        };
    }

    public ModelRuntimeStatusRenderPlan LoadedStatus(ModelRuntimeStatusDisplay? status)
        => status is null
            ? ModelRuntimeStatusRenderPlan.None
            : Render(status, updateProgress: true, includeStatusText: false);

    private static ModelRuntimeStatusRenderPlan Render(
        ModelRuntimeStatusDisplay status,
        bool updateProgress,
        bool includeStatusText)
        => new(
            ShouldRender: true,
            status.MetricText,
            updateProgress,
            includeStatusText ? status.StatusText ?? "" : "");
}
