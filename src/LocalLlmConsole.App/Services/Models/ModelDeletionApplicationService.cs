namespace LocalLlmConsole.Services;

public enum ModelDeletionApplicationOutcome
{
    Ignored,
    BlockedLoaded,
    Cancelled,
    Deleted
}

public sealed record ModelDeletionConfirmation(
    string Title,
    string Message);

public sealed record ModelDeletionApplicationActions(
    Func<ModelRecord, bool> IsModelLoaded,
    Func<ModelDeletionConfirmation, bool> ConfirmDelete,
    Func<string, Func<Task>, Task> RunBusyAsync,
    Func<ModelRecord, string, Task> DeleteModelAsync,
    Func<Task> RefreshModelsAsync,
    Func<Task> RefreshOverviewAsync,
    Action<string> SetStatus);

public sealed class ModelDeletionApplicationService
{
    private const string BusyMessage = "Removing model...";

    public async Task<ModelDeletionApplicationOutcome> DeleteAsync(
        ModelRecord? model,
        string modelsRoot,
        ModelDeletionApplicationActions actions)
    {
        Validate(actions);
        if (model is null)
            return ModelDeletionApplicationOutcome.Ignored;

        if (actions.IsModelLoaded(model))
        {
            actions.SetStatus("Unload the selected model before deleting it.");
            return ModelDeletionApplicationOutcome.BlockedLoaded;
        }

        var confirmation = BuildConfirmation(model);
        if (!actions.ConfirmDelete(confirmation))
            return ModelDeletionApplicationOutcome.Cancelled;

        await actions.RunBusyAsync(BusyMessage, async () =>
        {
            await actions.DeleteModelAsync(model, modelsRoot);
            await actions.RefreshModelsAsync();
            await actions.RefreshOverviewAsync();
        });
        return ModelDeletionApplicationOutcome.Deleted;
    }

    public static ModelDeletionConfirmation BuildConfirmation(ModelRecord model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var action = ModelAliasService.IsLaunchAlias(model)
            ? "remove this saved model variant without deleting the GGUF file"
            : model.Ownership == OwnershipKind.AppOwned
                ? "delete the downloaded model files"
                : "remove the model registration only";

        return new ModelDeletionConfirmation(
            "Remove model",
            $"This will {action} for:{Environment.NewLine}{Environment.NewLine}{model.Name}");
    }

    private static void Validate(ModelDeletionApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.IsModelLoaded);
        ArgumentNullException.ThrowIfNull(actions.ConfirmDelete);
        ArgumentNullException.ThrowIfNull(actions.RunBusyAsync);
        ArgumentNullException.ThrowIfNull(actions.DeleteModelAsync);
        ArgumentNullException.ThrowIfNull(actions.RefreshModelsAsync);
        ArgumentNullException.ThrowIfNull(actions.RefreshOverviewAsync);
        ArgumentNullException.ThrowIfNull(actions.SetStatus);
    }
}
