namespace LocalLlmConsole.Services;

public sealed record OpenCodeLocalModelDraftApplicationActions(
    Action<string> SetModelSnippetText,
    Action UpdateModelEditorState,
    Action<string> SetAddModelStatus,
    Action<OpenCodeLocalModelActionState> ApplyLocalModelActionState,
    Action<bool, bool> UpdateExistingModelButtons);

public sealed record OpenCodeLocalModelSaveApplicationActions(
    Func<string, Task> RefreshOpenCodeAsync,
    Action<string> SetStatus);

public enum OpenCodeLocalModelAddStateOutcome
{
    Skipped,
    NoLocalModelSelected,
    Analyzed,
    Failed
}

public sealed record OpenCodeLocalModelAddStateRequest(
    bool IsAddNewModelSelected,
    string ConfigPath,
    ModelRecord? SelectedLocalModel,
    string Snippet,
    bool UseGatewayProvider);

public sealed record OpenCodeLocalModelAddStateActions(
    Action<OpenCodeLocalModelActionState> ApplyLocalModelActionState);

public enum OpenCodeLocalModelSnippetSaveOutcome
{
    NoLocalModelSelected,
    Saved
}

public sealed record OpenCodeLocalModelSnippetSaveRequest(
    ModelRecord? SelectedLocalModel,
    OpenCodeFileSet Files,
    AppSettings ApplicationSettings,
    LoadedModelSessionSnapshot? LoadedSession,
    string Snippet,
    bool AddAsNew,
    bool UseGatewayProvider);

public sealed record OpenCodeLocalModelSnippetSaveActions(
    OpenCodeLaunchProfileEnsurer EnsureProfileAsync,
    OpenCodeApiKeyEnsurer EnsureApiKeyAsync,
    OpenCodeLocalModelSaveApplicationActions ResultActions);

public enum OpenCodeLocalModelDraftLoadOutcome
{
    Skipped,
    NoLocalModelSelected,
    StaleSelection,
    DraftLoaded,
    Failed
}

public sealed record OpenCodeLocalModelDraftLoadRequest(
    bool IsAddNewModelSelected,
    bool HasModelSnippetBox,
    ModelRecord? SelectedLocalModel,
    OpenCodeFileSet Files,
    AppSettings ApplicationSettings,
    LoadedModelSessionSnapshot? LoadedSession,
    bool UseGatewayProvider);

public sealed record OpenCodeLocalModelDraftLoadActions(
    Func<ModelRecord?> SelectedLocalModel,
    OpenCodeLaunchProfileEnsurer EnsureProfileAsync,
    OpenCodeApiKeyEnsurer EnsureApiKeyAsync,
    OpenCodeModelCapabilityReader ReadCapabilitiesAsync,
    OpenCodeLocalModelDraftApplicationActions DraftActions);

public sealed class OpenCodeLocalModelApplicationService
{
    private readonly OpenCodeLocalModelWorkflowService _workflow;

    public OpenCodeLocalModelApplicationService(OpenCodeLocalModelWorkflowService workflow)
    {
        _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
    }

    public OpenCodeLocalModelAddStateOutcome UpdateAddState(
        OpenCodeLocalModelAddStateRequest request,
        OpenCodeLocalModelAddStateActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(actions);

        if (!request.IsAddNewModelSelected)
            return OpenCodeLocalModelAddStateOutcome.Skipped;

        if (request.SelectedLocalModel is not { } model)
        {
            actions.ApplyLocalModelActionState(_workflow.NoLocalModelSelected());
            return OpenCodeLocalModelAddStateOutcome.NoLocalModelSelected;
        }

        try
        {
            actions.ApplyLocalModelActionState(_workflow.AnalyzeLocalModelSnippet(
                request.ConfigPath,
                model,
                request.Snippet,
                request.UseGatewayProvider));
            return OpenCodeLocalModelAddStateOutcome.Analyzed;
        }
        catch (Exception ex)
        {
            actions.ApplyLocalModelActionState(new OpenCodeLocalModelActionState(
                ex.Message,
                AddVisible: true,
                AddEnabled: false,
                UpdateVisible: false,
                UpdateEnabled: false,
                AddAsNewVisible: false,
                AddAsNewEnabled: false));
            return OpenCodeLocalModelAddStateOutcome.Failed;
        }
    }

    public async Task<OpenCodeLocalModelDraftLoadOutcome> LoadDraftAsync(
        OpenCodeLocalModelDraftLoadRequest request,
        OpenCodeLocalModelDraftLoadActions actions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.SelectedLocalModel);
        ArgumentNullException.ThrowIfNull(actions.EnsureProfileAsync);
        ArgumentNullException.ThrowIfNull(actions.EnsureApiKeyAsync);
        ArgumentNullException.ThrowIfNull(actions.ReadCapabilitiesAsync);
        ArgumentNullException.ThrowIfNull(actions.DraftActions);

        if (!request.IsAddNewModelSelected || !request.HasModelSnippetBox)
            return OpenCodeLocalModelDraftLoadOutcome.Skipped;

        if (request.SelectedLocalModel is not { } model)
        {
            ApplyNoLocalModelSelected(_workflow.NoLocalModelSelected(), actions.DraftActions);
            return OpenCodeLocalModelDraftLoadOutcome.NoLocalModelSelected;
        }

        try
        {
            var selectedModelId = model.Id;
            var launchSettings = await _workflow.ResolveLaunchSettingsAsync(new OpenCodeLocalModelLaunchSettingsRequest(
                model,
                request.ApplicationSettings,
                request.LoadedSession,
                actions.EnsureProfileAsync,
                actions.EnsureApiKeyAsync),
                cancellationToken);
            if (!IsSelectedModel(actions.SelectedLocalModel(), selectedModelId))
                return OpenCodeLocalModelDraftLoadOutcome.StaleSelection;

            var draft = await _workflow.CreateDraftAsync(
                new OpenCodeLocalModelDraftBuildRequest(
                    request.Files,
                    model,
                    request.ApplicationSettings,
                    launchSettings,
                    request.UseGatewayProvider),
                actions.ReadCapabilitiesAsync,
                cancellationToken);
            if (!IsSelectedModel(actions.SelectedLocalModel(), selectedModelId))
                return OpenCodeLocalModelDraftLoadOutcome.StaleSelection;

            ApplyDraftLoaded(draft, actions.DraftActions);
            return OpenCodeLocalModelDraftLoadOutcome.DraftLoaded;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            ApplyDraftLoadFailure(ex.Message, actions.DraftActions);
            return OpenCodeLocalModelDraftLoadOutcome.Failed;
        }
    }

    public void ApplyNoLocalModelSelected(
        OpenCodeLocalModelActionState state,
        OpenCodeLocalModelDraftApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(actions);

        actions.SetModelSnippetText("");
        actions.ApplyLocalModelActionState(state);
        actions.UpdateExistingModelButtons(false, true);
    }

    public void ApplyDraftLoaded(
        OpenCodeLocalModelDraft draft,
        OpenCodeLocalModelDraftApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(actions);

        actions.SetModelSnippetText(draft.Snippet);
        actions.UpdateModelEditorState();
    }

    public void ApplyDraftLoadFailure(
        string message,
        OpenCodeLocalModelDraftApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        actions.SetAddModelStatus(message);
        actions.ApplyLocalModelActionState(new OpenCodeLocalModelActionState(
            message,
            AddVisible: true,
            AddEnabled: false,
            UpdateVisible: false,
            UpdateEnabled: false,
            AddAsNewVisible: false,
            AddAsNewEnabled: false));
        actions.UpdateExistingModelButtons(false, true);
    }

    public async Task ApplySaveResultAsync(
        OpenCodeLocalModelSaveResult result,
        OpenCodeLocalModelSaveApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(result);
        Validate(actions);

        await actions.RefreshOpenCodeAsync(result.FullId);
        actions.SetStatus(result.StatusMessage);
    }

    public async Task<OpenCodeLocalModelSnippetSaveOutcome> SaveSnippetAsync(
        OpenCodeLocalModelSnippetSaveRequest request,
        OpenCodeLocalModelSnippetSaveActions actions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(actions);

        if (request.SelectedLocalModel is not { } model)
        {
            actions.ResultActions.SetStatus("Choose a local model to add.");
            return OpenCodeLocalModelSnippetSaveOutcome.NoLocalModelSelected;
        }

        var launchSettings = await _workflow.ResolveLaunchSettingsAsync(new OpenCodeLocalModelLaunchSettingsRequest(
            model,
            request.ApplicationSettings,
            request.LoadedSession,
            actions.EnsureProfileAsync,
            actions.EnsureApiKeyAsync),
            cancellationToken);
        var result = _workflow.Save(new OpenCodeLocalModelSaveWorkflowRequest(
            request.Files,
            model,
            request.ApplicationSettings,
            launchSettings,
            request.Snippet,
            request.AddAsNew,
            request.UseGatewayProvider));
        await ApplySaveResultAsync(result, actions.ResultActions);
        return OpenCodeLocalModelSnippetSaveOutcome.Saved;
    }

    public Task<OpenCodeModelLimits> ResolveLimitsAsync(
        ModelRecord model,
        AppSettings launchSettings,
        OpenCodeModelCapabilityReader readCapabilitiesAsync,
        CancellationToken cancellationToken = default)
        => _workflow.ResolveLimitsAsync(model, launchSettings, readCapabilitiesAsync, cancellationToken);

    private static void Validate(OpenCodeLocalModelAddStateActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.ApplyLocalModelActionState);
    }

    private static void Validate(OpenCodeLocalModelSnippetSaveActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.EnsureProfileAsync);
        ArgumentNullException.ThrowIfNull(actions.EnsureApiKeyAsync);
        ArgumentNullException.ThrowIfNull(actions.ResultActions);
        ArgumentNullException.ThrowIfNull(actions.ResultActions.RefreshOpenCodeAsync);
        ArgumentNullException.ThrowIfNull(actions.ResultActions.SetStatus);
    }

    private static void Validate(OpenCodeLocalModelSaveApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.RefreshOpenCodeAsync);
        ArgumentNullException.ThrowIfNull(actions.SetStatus);
    }

    private static bool IsSelectedModel(ModelRecord? selected, string expectedModelId)
        => selected is not null
            && string.Equals(selected.Id, expectedModelId, StringComparison.OrdinalIgnoreCase);
}
