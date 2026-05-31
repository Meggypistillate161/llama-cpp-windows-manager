namespace LocalLlmConsole.Services;

public sealed record OpenCodeExistingModelEditorState(
    bool SaveVisible,
    bool DeleteVisible,
    bool SaveEnabled,
    bool DeleteEnabled,
    string SaveContent);

public sealed record OpenCodeModelSaveResult(
    string FullId,
    string StatusMessage);

public sealed record OpenCodeModelDeleteResult(
    string StatusMessage);

public sealed class OpenCodeModelWorkflowService
{
    private readonly OpenCodeConfigService _openCode;

    public OpenCodeModelWorkflowService(OpenCodeConfigService openCode)
    {
        _openCode = openCode ?? throw new ArgumentNullException(nameof(openCode));
    }

    public OpenCodeExistingModelEditorState ExistingModelEditorState(OpenCodeModelEntry? selected, bool snippetValid, bool matchesSaved)
    {
        var adding = selected?.IsAddNew ?? true;
        return new OpenCodeExistingModelEditorState(
            SaveVisible: !adding,
            DeleteVisible: !adding,
            SaveEnabled: !adding && snippetValid && !matchesSaved,
            DeleteEnabled: selected is not null && !adding,
            SaveContent: matchesSaved ? "Saved" : "Update Config");
    }

    public string ReadModelSnippet(OpenCodeFileSet files, OpenCodeModelEntry model)
        => _openCode.ReadModelSnippet(files.ConfigPath, model);

    public bool SnippetsEquivalent(string savedSnippet, string editedSnippet)
        => _openCode.SnippetsEquivalent(savedSnippet, editedSnippet);

    public string AlreadySavedStatus(OpenCodeModelEntry model)
        => $"OpenCode model {model.FullId} is already saved.";

    public OpenCodeModelCommandAdmission SaveAdmission(OpenCodeModelEntry? model)
        => EditableModelAdmission(model, Confirmation: null);

    public OpenCodeModelCommandAdmission DeleteAdmission(OpenCodeModelEntry? model)
        => EditableModelAdmission(
            model,
            model is null || model.IsAddNew
                ? null
                : new OpenCodeCommandConfirmation(
                    "Delete OpenCode model",
                    $"Delete this OpenCode model config?\n\n{model.Label}"));

    public OpenCodeModelSaveResult SaveModelSnippet(OpenCodeFileSet files, OpenCodeModelEntry model, string snippet)
    {
        _openCode.SaveModelSnippet(files.ConfigPath, model, snippet);
        return new OpenCodeModelSaveResult(model.FullId, $"Saved OpenCode model {model.FullId}.");
    }

    public OpenCodeModelDeleteResult DeleteModel(OpenCodeFileSet files, OpenCodeModelEntry model)
    {
        _openCode.DeleteModel(files.ConfigPath, model);
        return new OpenCodeModelDeleteResult($"Deleted OpenCode model {model.FullId}.");
    }

    private static OpenCodeModelCommandAdmission EditableModelAdmission(
        OpenCodeModelEntry? model,
        OpenCodeCommandConfirmation? Confirmation)
        => model is null || model.IsAddNew
            ? new OpenCodeModelCommandAdmission(null, "Choose an OpenCode model first.", Confirmation: null)
            : new OpenCodeModelCommandAdmission(model, "", Confirmation);
}
