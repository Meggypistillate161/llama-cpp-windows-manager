namespace LocalLlmConsole.Services;

public sealed record ModelLaunchVariantSaveApplicationRequest(
    ModelLaunchVariantWorkflowResult Result,
    AppSettings Settings);

public enum ModelLaunchVariantSaveApplicationOutcome
{
    NoModelSelected,
    Failed,
    Saved
}

public sealed record ModelLaunchVariantSaveActions(
    Func<Task> RefreshModelsAsync,
    Action<string> SelectModelAfterRefresh,
    Func<Task> RenderSelectedModelLaunchSettingsAsync,
    Func<Task> RefreshOverviewModelSelectorAsync,
    Func<AppSettings, Task> SyncOpenCodeLocalProviderAsync,
    Action<string> SetStatus);

public sealed record ModelLaunchVariantSaveSelectedActions(
    Func<string, Func<Task>, Task> RunBusyAsync,
    Func<string, bool> IsEditorLoadedForModel,
    Func<Task> RenderSelectedModelLaunchSettingsAsync,
    Func<AppSettings> ReadLaunchSettings,
    Func<string> SelectedRuntimeId,
    Func<ModelLaunchVariantWorkflowRequest, Task<ModelLaunchVariantWorkflowResult>> SaveAsNewAsync,
    ModelLaunchVariantSaveActions ResultActions);

public sealed class ModelLaunchVariantSaveApplicationService
{
    public async Task<ModelLaunchVariantSaveApplicationOutcome> SaveSelectedAsNewAsync(
        ModelRecord? source,
        string requestedName,
        AppSettings settings,
        ModelLaunchVariantSaveSelectedActions actions)
    {
        Validate(actions);

        if (source is null)
        {
            actions.ResultActions.SetStatus("Select a model before saving a new model variant.");
            return ModelLaunchVariantSaveApplicationOutcome.NoModelSelected;
        }

        var saved = false;
        await actions.RunBusyAsync("Saving model variant...", async () =>
        {
            if (!actions.IsEditorLoadedForModel(source.Id))
                await actions.RenderSelectedModelLaunchSettingsAsync();

            var launchSettings = actions.ReadLaunchSettings();
            var result = await actions.SaveAsNewAsync(new ModelLaunchVariantWorkflowRequest(
                source,
                requestedName,
                launchSettings,
                actions.SelectedRuntimeId(),
                settings));
            saved = await ApplyAsync(
                new ModelLaunchVariantSaveApplicationRequest(result, settings),
                actions.ResultActions);
        });

        return saved
            ? ModelLaunchVariantSaveApplicationOutcome.Saved
            : ModelLaunchVariantSaveApplicationOutcome.Failed;
    }

    public async Task<bool> ApplyAsync(
        ModelLaunchVariantSaveApplicationRequest request,
        ModelLaunchVariantSaveActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        var result = request.Result ?? throw new ArgumentNullException(nameof(request.Result));
        if (!result.Success || result.Alias is null)
        {
            actions.SetStatus(result.StatusMessage);
            return false;
        }

        await actions.RefreshModelsAsync();
        actions.SelectModelAfterRefresh(result.Alias.Id);
        await actions.RenderSelectedModelLaunchSettingsAsync();
        await actions.RefreshOverviewModelSelectorAsync();
        if (request.Settings.AutoSaveOpenCodeOnLaunchSettingsSave)
            await actions.SyncOpenCodeLocalProviderAsync(request.Settings);
        actions.SetStatus(result.StatusMessage);
        return true;
    }

    private static void Validate(ModelLaunchVariantSaveSelectedActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.RunBusyAsync);
        ArgumentNullException.ThrowIfNull(actions.IsEditorLoadedForModel);
        ArgumentNullException.ThrowIfNull(actions.RenderSelectedModelLaunchSettingsAsync);
        ArgumentNullException.ThrowIfNull(actions.ReadLaunchSettings);
        ArgumentNullException.ThrowIfNull(actions.SelectedRuntimeId);
        ArgumentNullException.ThrowIfNull(actions.SaveAsNewAsync);
        Validate(actions.ResultActions);
    }

    private static void Validate(ModelLaunchVariantSaveActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.RefreshModelsAsync);
        ArgumentNullException.ThrowIfNull(actions.SelectModelAfterRefresh);
        ArgumentNullException.ThrowIfNull(actions.RenderSelectedModelLaunchSettingsAsync);
        ArgumentNullException.ThrowIfNull(actions.RefreshOverviewModelSelectorAsync);
        ArgumentNullException.ThrowIfNull(actions.SyncOpenCodeLocalProviderAsync);
        ArgumentNullException.ThrowIfNull(actions.SetStatus);
    }
}
