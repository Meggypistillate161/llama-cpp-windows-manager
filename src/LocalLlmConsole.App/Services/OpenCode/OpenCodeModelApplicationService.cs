namespace LocalLlmConsole.Services;

public enum OpenCodeModelCommandOutcome
{
    Rejected,
    AlreadySaved,
    Saved,
    Deleted
}

public enum OpenCodeModelLoadOutcome
{
    NoEditor,
    Adding,
    Loaded,
    Failed
}

public sealed record OpenCodeModelLoadApplicationRequest(
    OpenCodeFileSet Files,
    OpenCodeModelEntry? SelectedModel,
    bool HasSnippetEditor);

public sealed record OpenCodeModelLoadSelectedApplicationActions(
    Func<Task> LoadLocalModelDraftAsync,
    OpenCodeModelLoadApplicationActions LoadActions);

public sealed record OpenCodeModelSaveApplicationRequest(
    OpenCodeFileSet Files,
    OpenCodeModelEntry? SelectedModel,
    string Snippet,
    string SavedSnippet);

public sealed record OpenCodeModelDeleteApplicationRequest(
    OpenCodeFileSet Files,
    OpenCodeModelEntry? SelectedModel);

public sealed record OpenCodeModelEditorStateApplicationRequest(
    OpenCodeModelEntry? SelectedModel,
    string CurrentSnippet,
    string SavedSnippet);

public sealed record OpenCodeModelEditorStateApplicationResult(
    OpenCodeExistingModelEditorState ExistingModelState,
    bool RefreshLocalModelAddState);

public sealed record OpenCodeModelSaveApplicationActions(
    Func<string, Func<Task>, Task> RunBusyAsync,
    Func<OpenCodeCommandConfirmation, bool> Confirm,
    OpenCodeModelCommandApplicationActions ResultActions);

public sealed record OpenCodeModelDeleteApplicationActions(
    Func<string, Func<Task>, Task> RunBusyAsync,
    Func<OpenCodeCommandConfirmation, bool> Confirm,
    OpenCodeModelCommandApplicationActions ResultActions);

public sealed class OpenCodeModelApplicationService
{
    private readonly OpenCodeModelWorkflowService _workflow;

    public OpenCodeModelApplicationService(OpenCodeModelWorkflowService workflow)
    {
        _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
    }

    public async Task<OpenCodeModelLoadOutcome> LoadSelectedAsync(
        OpenCodeModelLoadApplicationRequest request,
        OpenCodeModelLoadSelectedApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(actions);

        var adding = ApplyModelChrome(request.SelectedModel, actions.LoadActions);
        if (!request.HasSnippetEditor)
            return OpenCodeModelLoadOutcome.NoEditor;

        if (adding)
        {
            ApplyModelAdding(actions.LoadActions);
            await actions.LoadLocalModelDraftAsync();
            return OpenCodeModelLoadOutcome.Adding;
        }

        try
        {
            var snippet = _workflow.ReadModelSnippet(request.Files, request.SelectedModel!);
            ApplyModelSnippetLoaded(snippet, actions.LoadActions);
            return OpenCodeModelLoadOutcome.Loaded;
        }
        catch (Exception ex)
        {
            ApplyModelSnippetLoadFailure(ex.Message, actions.LoadActions);
            return OpenCodeModelLoadOutcome.Failed;
        }
    }

    public async Task<OpenCodeModelCommandOutcome> SaveSnippetAsync(
        OpenCodeModelSaveApplicationRequest request,
        OpenCodeModelSaveApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(actions);

        var admission = _workflow.SaveAdmission(request.SelectedModel);
        if (!TryAdmit(admission, actions.ResultActions, actions.Confirm, out var model))
            return OpenCodeModelCommandOutcome.Rejected;

        if (_workflow.SnippetsEquivalent(request.SavedSnippet, request.Snippet))
        {
            actions.ResultActions.UpdateModelEditorState();
            actions.ResultActions.SetStatus(_workflow.AlreadySavedStatus(model));
            return OpenCodeModelCommandOutcome.AlreadySaved;
        }

        await actions.RunBusyAsync("Saving OpenCode model snippet...", async () =>
        {
            var result = _workflow.SaveModelSnippet(request.Files, model, request.Snippet);
            await actions.ResultActions.RefreshModelAsync(result.FullId);
            actions.ResultActions.SetStatus(result.StatusMessage);
        });
        return OpenCodeModelCommandOutcome.Saved;
    }

    public async Task<OpenCodeModelCommandOutcome> DeleteAsync(
        OpenCodeModelDeleteApplicationRequest request,
        OpenCodeModelDeleteApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(actions);

        var admission = _workflow.DeleteAdmission(request.SelectedModel);
        if (!TryAdmit(admission, actions.ResultActions, actions.Confirm, out var model))
            return OpenCodeModelCommandOutcome.Rejected;

        await actions.RunBusyAsync("Deleting OpenCode model config...", async () =>
        {
            var result = _workflow.DeleteModel(request.Files, model);
            await actions.ResultActions.RefreshOpenCodeAsync();
            actions.ResultActions.SetStatus(result.StatusMessage);
        });
        return OpenCodeModelCommandOutcome.Deleted;
    }

    public OpenCodeExistingModelEditorState ExistingModelEditorState(
        OpenCodeModelEntry? selected,
        bool snippetValid,
        bool matchesSaved)
        => _workflow.ExistingModelEditorState(selected, snippetValid, matchesSaved);

    public OpenCodeModelEditorStateApplicationResult EditorState(OpenCodeModelEditorStateApplicationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var adding = request.SelectedModel?.IsAddNew ?? true;
        if (adding)
        {
            return new OpenCodeModelEditorStateApplicationResult(
                _workflow.ExistingModelEditorState(request.SelectedModel, snippetValid: false, matchesSaved: true),
                RefreshLocalModelAddState: true);
        }

        try
        {
            var matchesSaved = _workflow.SnippetsEquivalent(request.SavedSnippet, request.CurrentSnippet);
            return new OpenCodeModelEditorStateApplicationResult(
                _workflow.ExistingModelEditorState(request.SelectedModel, snippetValid: true, matchesSaved),
                RefreshLocalModelAddState: false);
        }
        catch
        {
            return new OpenCodeModelEditorStateApplicationResult(
                _workflow.ExistingModelEditorState(request.SelectedModel, snippetValid: false, matchesSaved: false),
                RefreshLocalModelAddState: false);
        }
    }

    private static bool ApplyModelChrome(OpenCodeModelEntry? model, OpenCodeModelLoadApplicationActions actions)
    {
        var adding = model?.IsAddNew ?? true;
        actions.SetAddModelPanelVisible(adding);
        actions.SetDeleteModelButtonVisible(!adding);
        return adding;
    }

    private static void ApplyModelAdding(OpenCodeModelLoadApplicationActions actions)
    {
        actions.ClearSavedSnippet();
        actions.UpdateModelEditorState();
    }

    private static void ApplyModelSnippetLoaded(string snippet, OpenCodeModelLoadApplicationActions actions)
    {
        actions.SetSavedSnippet(snippet);
        actions.SetModelSnippetText(snippet);
        actions.UpdateModelEditorState();
    }

    private static void ApplyModelSnippetLoadFailure(string message, OpenCodeModelLoadApplicationActions actions)
    {
        actions.ClearSavedSnippet();
        actions.SetModelSnippetText(message);
        actions.UpdateModelEditorState();
        actions.SetStatus(message);
    }

    private static bool TryAdmit(
        OpenCodeModelCommandAdmission admission,
        OpenCodeModelCommandApplicationActions actions,
        Func<OpenCodeCommandConfirmation, bool> confirm,
        out OpenCodeModelEntry model)
    {
        ArgumentNullException.ThrowIfNull(admission);

        if (!string.IsNullOrWhiteSpace(admission.StatusMessage))
        {
            actions.SetStatus(admission.StatusMessage);
            model = default!;
            return false;
        }

        if (admission.Model is not { } admittedModel)
        {
            actions.SetStatus("Choose an OpenCode model first.");
            model = default!;
            return false;
        }

        if (admission.Confirmation is not null && !confirm(admission.Confirmation))
        {
            model = default!;
            return false;
        }

        model = admittedModel;
        return true;
    }

    private static void Validate(OpenCodeModelLoadSelectedApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.LoadLocalModelDraftAsync);
        Validate(actions.LoadActions);
    }

    private static void Validate(OpenCodeModelLoadApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.SetAddModelPanelVisible);
        ArgumentNullException.ThrowIfNull(actions.SetDeleteModelButtonVisible);
        ArgumentNullException.ThrowIfNull(actions.ClearSavedSnippet);
        ArgumentNullException.ThrowIfNull(actions.SetSavedSnippet);
        ArgumentNullException.ThrowIfNull(actions.SetModelSnippetText);
        ArgumentNullException.ThrowIfNull(actions.UpdateModelEditorState);
        ArgumentNullException.ThrowIfNull(actions.SetStatus);
    }

    private static void Validate(OpenCodeModelSaveApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.RunBusyAsync);
        ArgumentNullException.ThrowIfNull(actions.Confirm);
        Validate(actions.ResultActions);
    }

    private static void Validate(OpenCodeModelDeleteApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.RunBusyAsync);
        ArgumentNullException.ThrowIfNull(actions.Confirm);
        Validate(actions.ResultActions);
    }

    private static void Validate(OpenCodeModelCommandApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.UpdateModelEditorState);
        ArgumentNullException.ThrowIfNull(actions.RefreshModelAsync);
        ArgumentNullException.ThrowIfNull(actions.RefreshOpenCodeAsync);
        ArgumentNullException.ThrowIfNull(actions.SetStatus);
    }
}
