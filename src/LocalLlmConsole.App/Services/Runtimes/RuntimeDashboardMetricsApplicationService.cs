namespace LocalLlmConsole.Services;

public sealed record RuntimeMetricSummaryPresentation(
    string GenerationRate,
    string TotalTokens,
    string Settings,
    DateTimeOffset? LastKnownCapturedAt)
{
    public static RuntimeMetricSummaryPresentation NoRuntime { get; } = new(
        "No runtime",
        "0",
        "Context No runtime\nKV cache No runtime",
        LastKnownCapturedAt: null);
}

public sealed record RuntimeDashboardMetricsApplicationRequest(
    bool RenderOverview,
    LoadedModelSessionSnapshot? SelectedSession,
    AppSettings MetricsSettings,
    RuntimeMetricPollResult? SelectedPollResult,
    string RuntimeKey);

public sealed record RuntimeDashboardMetricsApplicationActions(
    Action<RuntimeSlotSnapshot?> RefreshRuntimeLogTail,
    Action<RuntimeMetricRowsRenderPlan> ApplyMetricRows,
    Action<RuntimeMetricSummaryPresentation> ApplyMetricSummary);

public sealed class RuntimeDashboardMetricsApplicationService
{
    private readonly RuntimeTelemetryApplicationService _telemetry;
    private readonly RuntimeDashboardRenderDecisionService _renderDecisions;
    private readonly RuntimeMetricRowsRenderService _rowsRender;

    public RuntimeDashboardMetricsApplicationService(
        RuntimeTelemetryApplicationService telemetry,
        RuntimeDashboardRenderDecisionService renderDecisions,
        RuntimeMetricRowsRenderService rowsRender)
    {
        _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
        _renderDecisions = renderDecisions ?? throw new ArgumentNullException(nameof(renderDecisions));
        _rowsRender = rowsRender ?? throw new ArgumentNullException(nameof(rowsRender));
    }

    public RuntimeDashboardRenderDecisionKind Apply(
        RuntimeDashboardMetricsApplicationRequest request,
        RuntimeDashboardMetricsApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(actions);
        ArgumentNullException.ThrowIfNull(request.MetricsSettings);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RuntimeKey);

        var decision = _renderDecisions.Decide(new RuntimeDashboardRenderDecisionRequest(
            request.SelectedSession,
            request.MetricsSettings,
            request.SelectedPollResult));

        if (decision.Kind == RuntimeDashboardRenderDecisionKind.NoRuntime)
        {
            _telemetry.ResetMetricCounters();
            if (request.RenderOverview)
            {
                actions.RefreshRuntimeLogTail(null);
                actions.ApplyMetricRows(_rowsRender.FromSamples([]));
                actions.ApplyMetricSummary(RuntimeMetricSummaryPresentation.NoRuntime);
            }
            return decision.Kind;
        }

        if (request.RenderOverview)
            actions.RefreshRuntimeLogTail(decision.SlotSnapshot);

        if (decision.Kind == RuntimeDashboardRenderDecisionKind.MetricsDisabled)
        {
            _telemetry.ResetMetricCounters();
            if (request.RenderOverview)
                actions.ApplyMetricRows(_rowsRender.FromSamples([]));
            actions.ApplyMetricSummary(BuildSummary(request.RuntimeKey, [], request.MetricsSettings, decision.SlotSnapshot));
            return decision.Kind;
        }

        if (decision.Kind == RuntimeDashboardRenderDecisionKind.FreshMetrics)
        {
            if (request.RenderOverview)
                actions.ApplyMetricRows(_rowsRender.FromSamples(decision.Samples));
            actions.ApplyMetricSummary(BuildSummary(request.RuntimeKey, decision.Samples, request.MetricsSettings, decision.SlotSnapshot));
            return decision.Kind;
        }

        if (request.RenderOverview)
        {
            actions.ApplyMetricRows(_rowsRender.Unavailable(
                decision.Error,
                _telemetry.LastKnownSamples(request.RuntimeKey)));
        }
        actions.ApplyMetricSummary(BuildSummary(request.RuntimeKey, [], request.MetricsSettings, decision.SlotSnapshot));
        return decision.Kind;
    }

    private RuntimeMetricSummaryPresentation BuildSummary(
        string runtimeKey,
        IReadOnlyList<PrometheusSample> samples,
        AppSettings metricsSettings,
        RuntimeSlotSnapshot? slotSnapshot)
    {
        var summary = _telemetry.ApplyMetricSummary(runtimeKey, samples, metricsSettings, slotSnapshot);
        return new RuntimeMetricSummaryPresentation(
            summary.GenerationRate,
            summary.TotalTokens,
            summary.Settings,
            summary.UsedLastKnown ? summary.LastKnownCapturedAt : null);
    }

    private static void Validate(RuntimeDashboardMetricsApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.RefreshRuntimeLogTail);
        ArgumentNullException.ThrowIfNull(actions.ApplyMetricRows);
        ArgumentNullException.ThrowIfNull(actions.ApplyMetricSummary);
    }
}
