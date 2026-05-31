namespace LocalLlmConsole.Services;

public enum OverviewLoadedSessionSelectionOutcome
{
    Ignored,
    Stale,
    Selected
}

public sealed record OverviewLoadedSessionSelectionApplicationActions(
    Func<string, ModelRecord?> FindModel,
    Func<Task> RefreshOverviewModelSelectorAsync,
    Action<string> SelectModelId,
    Func<string, RuntimeSessionSelectResult> SelectRuntimeModel,
    Action<AppSettings?> SetActiveRuntimeSettings,
    Action ResetMetricCounters,
    Func<Task> SaveActiveRuntimeSessionsAsync,
    Func<Task> RefreshRuntimeMetricsAsync,
    Action UpdateOverviewModelActions,
    Action<string> SetStatus);

public sealed class OverviewLoadedSessionSelectionApplicationService
{
    public async Task<OverviewLoadedSessionSelectionOutcome> SelectAsync(
        string? modelId,
        OverviewLoadedSessionSelectionApplicationActions actions)
    {
        Validate(actions);

        if (string.IsNullOrWhiteSpace(modelId))
            return OverviewLoadedSessionSelectionOutcome.Ignored;

        var model = actions.FindModel(modelId);
        if (model is null)
        {
            await actions.RefreshOverviewModelSelectorAsync();
            model = actions.FindModel(modelId);
        }

        actions.SelectModelId(modelId);

        var selection = actions.SelectRuntimeModel(modelId);
        if (!selection.Selected)
        {
            actions.SetStatus("Selected session is no longer loaded.");
            return OverviewLoadedSessionSelectionOutcome.Stale;
        }

        actions.SetActiveRuntimeSettings(selection.ActiveSettings);
        actions.ResetMetricCounters();
        await actions.SaveActiveRuntimeSessionsAsync();
        await actions.RefreshRuntimeMetricsAsync();
        actions.UpdateOverviewModelActions();
        actions.SetStatus(model is null ? "Selected loaded model session." : $"Selected loaded model {model.Name}.");
        return OverviewLoadedSessionSelectionOutcome.Selected;
    }

    private static void Validate(OverviewLoadedSessionSelectionApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.FindModel);
        ArgumentNullException.ThrowIfNull(actions.RefreshOverviewModelSelectorAsync);
        ArgumentNullException.ThrowIfNull(actions.SelectModelId);
        ArgumentNullException.ThrowIfNull(actions.SelectRuntimeModel);
        ArgumentNullException.ThrowIfNull(actions.SetActiveRuntimeSettings);
        ArgumentNullException.ThrowIfNull(actions.ResetMetricCounters);
        ArgumentNullException.ThrowIfNull(actions.SaveActiveRuntimeSessionsAsync);
        ArgumentNullException.ThrowIfNull(actions.RefreshRuntimeMetricsAsync);
        ArgumentNullException.ThrowIfNull(actions.UpdateOverviewModelActions);
        ArgumentNullException.ThrowIfNull(actions.SetStatus);
    }
}
