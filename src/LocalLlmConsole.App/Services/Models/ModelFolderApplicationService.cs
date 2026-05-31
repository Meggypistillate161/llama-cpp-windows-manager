namespace LocalLlmConsole.Services;

public enum ModelFolderApplicationOutcome
{
    Ignored,
    Blocked,
    Opened
}

public sealed record ModelFolderApplicationActions(
    Action<string> OpenFolder,
    Action<string> SetStatus);

public sealed class ModelFolderApplicationService
{
    public ModelFolderApplicationOutcome Open(ModelRecord? model, ModelFolderApplicationActions actions)
    {
        Validate(actions);

        if (model is null)
            return ModelFolderApplicationOutcome.Ignored;

        var folder = Path.GetDirectoryName(model.ModelPath);
        if (string.IsNullOrWhiteSpace(folder))
        {
            actions.SetStatus("Model folder is unavailable.");
            return ModelFolderApplicationOutcome.Blocked;
        }

        actions.OpenFolder(folder);
        return ModelFolderApplicationOutcome.Opened;
    }

    private static void Validate(ModelFolderApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.OpenFolder);
        ArgumentNullException.ThrowIfNull(actions.SetStatus);
    }
}
