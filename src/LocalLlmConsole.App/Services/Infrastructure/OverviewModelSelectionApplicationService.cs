namespace LocalLlmConsole.Services;

public enum OverviewModelSelectionOutcome
{
    Ignored,
    NotLoaded,
    ActiveLoaded,
    SwitchedLoaded
}

public sealed record OverviewModelSelectionApplicationRequest(
    ModelRecord? Model,
    bool IsLoaded,
    bool IsActive);

public sealed record OverviewModelSelectionApplicationActions(
    Func<string, RuntimeSessionSelectResult> SelectModel,
    Action<AppSettings?> SetActiveRuntimeSettings,
    Func<Task> SaveActiveRuntimeSessionsAsync,
    Action ResetMetricCounters,
    Func<Task> RefreshRuntimeMetricsAsync,
    Action<string> SetStatus);

public sealed class OverviewModelSelectionApplicationService
{
    public async Task<OverviewModelSelectionOutcome> SelectAsync(
        OverviewModelSelectionApplicationRequest request,
        OverviewModelSelectionApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(actions);

        var model = request.Model;
        if (model is null)
            return OverviewModelSelectionOutcome.Ignored;

        var outcome = request.IsLoaded
            ? await SelectLoadedModelAsync(model, request.IsActive, actions)
            : OverviewModelSelectionOutcome.NotLoaded;

        actions.ResetMetricCounters();
        if (!request.IsLoaded)
            actions.SetStatus($"{model.Name} is not loaded. Load it to expose an OpenAI-compatible endpoint.");
        await actions.RefreshRuntimeMetricsAsync();

        return outcome;
    }

    private static async Task<OverviewModelSelectionOutcome> SelectLoadedModelAsync(
        ModelRecord model,
        bool isActive,
        OverviewModelSelectionApplicationActions actions)
    {
        if (isActive)
            return OverviewModelSelectionOutcome.ActiveLoaded;

        var selection = actions.SelectModel(model.Id);
        if (!selection.Selected)
        {
            actions.SetStatus("Selected model is no longer loaded.");
            return OverviewModelSelectionOutcome.NotLoaded;
        }

        actions.SetActiveRuntimeSettings(selection.ActiveSettings);
        await actions.SaveActiveRuntimeSessionsAsync();
        return OverviewModelSelectionOutcome.SwitchedLoaded;
    }

    private static void Validate(OverviewModelSelectionApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.SelectModel);
        ArgumentNullException.ThrowIfNull(actions.SetActiveRuntimeSettings);
        ArgumentNullException.ThrowIfNull(actions.SaveActiveRuntimeSessionsAsync);
        ArgumentNullException.ThrowIfNull(actions.ResetMetricCounters);
        ArgumentNullException.ThrowIfNull(actions.RefreshRuntimeMetricsAsync);
        ArgumentNullException.ThrowIfNull(actions.SetStatus);
    }
}
