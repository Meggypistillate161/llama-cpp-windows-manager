namespace LocalLlmConsole.Services;

public enum OpenCodeAgentCommandOutcome
{
    Rejected,
    Saved,
    Created,
    Deleted
}

public enum OpenCodeAgentLoadOutcome
{
    NoEditor,
    Adding,
    Loaded,
    Failed
}

public sealed record OpenCodeAgentLoadApplicationRequest(
    OpenCodeFileSet Files,
    OpenCodeAgentEntry? SelectedAgent,
    bool HasSnippetEditor);

public sealed record OpenCodeAgentSaveApplicationRequest(
    OpenCodeFileSet Files,
    OpenCodeAgentEntry? SelectedAgent,
    string Snippet);

public sealed record OpenCodeAgentCreateApplicationRequest(
    OpenCodeFileSet Files,
    string RequestedName,
    bool Markdown,
    IReadOnlyCollection<OpenCodeAgentEntry> ExistingAgents,
    OpenCodeModelEntry? SelectedModel);

public sealed record OpenCodeAgentDeleteApplicationRequest(
    OpenCodeFileSet Files,
    OpenCodeAgentEntry? SelectedAgent);

public sealed record OpenCodeAgentSaveApplicationActions(
    Func<string, Func<Task>, Task> RunBusyAsync,
    Func<OpenCodeCommandConfirmation, bool> Confirm,
    OpenCodeAgentCommandApplicationActions ResultActions);

public sealed record OpenCodeAgentCreateApplicationActions(
    Func<string, Func<Task>, Task> RunBusyAsync,
    Func<OpenCodeCommandConfirmation, bool> Confirm,
    OpenCodeAgentCommandApplicationActions ResultActions);

public sealed record OpenCodeAgentDeleteApplicationActions(
    Func<string, Func<Task>, Task> RunBusyAsync,
    Func<OpenCodeCommandConfirmation, bool> Confirm,
    OpenCodeAgentCommandApplicationActions ResultActions);

public sealed class OpenCodeAgentApplicationService
{
    private readonly OpenCodeAgentWorkflowService _workflow;

    public OpenCodeAgentApplicationService(OpenCodeAgentWorkflowService workflow)
    {
        _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
    }

    public Task<OpenCodeAgentLoadOutcome> LoadSelectedAsync(
        OpenCodeAgentLoadApplicationRequest request,
        OpenCodeAgentLoadApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(actions);

        var adding = ApplyAgentSelection(
            request.SelectedAgent,
            _workflow.AgentEditorState(request.SelectedAgent),
            actions);

        if (!request.HasSnippetEditor)
            return Task.FromResult(OpenCodeAgentLoadOutcome.NoEditor);

        if (adding)
            return Task.FromResult(OpenCodeAgentLoadOutcome.Adding);

        try
        {
            ApplyAgentSnippetLoaded(
                _workflow.ReadAgentSnippet(request.Files, request.SelectedAgent!),
                actions);
            return Task.FromResult(OpenCodeAgentLoadOutcome.Loaded);
        }
        catch (Exception ex)
        {
            ApplyAgentSnippetLoadFailure(ex.Message, actions);
            return Task.FromResult(OpenCodeAgentLoadOutcome.Failed);
        }
    }

    public async Task<OpenCodeAgentCommandOutcome> SaveSnippetAsync(
        OpenCodeAgentSaveApplicationRequest request,
        OpenCodeAgentSaveApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(actions);

        var admission = _workflow.SaveAdmission(request.SelectedAgent);
        if (!TryAdmit(admission, actions.ResultActions, actions.Confirm, out var agent))
            return OpenCodeAgentCommandOutcome.Rejected;

        await actions.RunBusyAsync("Saving OpenCode agent...", async () =>
        {
            var result = _workflow.SaveAgentSnippet(request.Files, agent, request.Snippet);
            await actions.ResultActions.RefreshAgentAsync(result.AgentId);
            actions.ResultActions.SetStatus(result.StatusMessage);
        });
        return OpenCodeAgentCommandOutcome.Saved;
    }

    public async Task<OpenCodeAgentCommandOutcome> CreateAsync(
        OpenCodeAgentCreateApplicationRequest request,
        OpenCodeAgentCreateApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(actions);

        var admission = _workflow.CreateAdmission(request.RequestedName, request.Markdown, request.ExistingAgents);
        if (!TryAdmit(admission, actions.ResultActions, actions.Confirm, out var draft))
            return OpenCodeAgentCommandOutcome.Rejected;

        await actions.RunBusyAsync("Creating OpenCode agent...", async () =>
        {
            var result = _workflow.CreateAgent(request.Files, draft, request.SelectedModel);
            await actions.ResultActions.RefreshAgentAsync(result.Agent.Id);
            actions.ResultActions.SetStatus(result.StatusMessage);
        });
        return OpenCodeAgentCommandOutcome.Created;
    }

    public async Task<OpenCodeAgentCommandOutcome> DeleteAsync(
        OpenCodeAgentDeleteApplicationRequest request,
        OpenCodeAgentDeleteApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(actions);

        var admission = _workflow.DeleteAdmission(request.SelectedAgent);
        if (!TryAdmit(admission, actions.ResultActions, actions.Confirm, out var agent))
            return OpenCodeAgentCommandOutcome.Rejected;

        await actions.RunBusyAsync("Deleting OpenCode agent...", async () =>
        {
            var result = _workflow.DeleteAgent(request.Files, agent);
            await actions.ResultActions.RefreshOpenCodeAsync();
            actions.ResultActions.SetStatus(result.StatusMessage);
        });
        return OpenCodeAgentCommandOutcome.Deleted;
    }

    private static bool ApplyAgentSelection(
        OpenCodeAgentEntry? agent,
        OpenCodeAgentEditorState editorState,
        OpenCodeAgentLoadApplicationActions actions)
    {
        actions.ApplyAgentEditorState(editorState);
        var adding = agent?.IsAddNew ?? true;
        if (adding)
            actions.SetAgentSnippetText("");
        return adding;
    }

    private static void ApplyAgentSnippetLoaded(string snippet, OpenCodeAgentLoadApplicationActions actions)
        => actions.SetAgentSnippetText(snippet);

    private static void ApplyAgentSnippetLoadFailure(string message, OpenCodeAgentLoadApplicationActions actions)
    {
        actions.SetAgentSnippetText(message);
        actions.SetStatus(message);
    }

    private static bool TryAdmit(
        OpenCodeAgentCommandAdmission admission,
        OpenCodeAgentCommandApplicationActions actions,
        Func<OpenCodeCommandConfirmation, bool> confirm,
        out OpenCodeAgentEntry agent)
    {
        ArgumentNullException.ThrowIfNull(admission);

        if (!string.IsNullOrWhiteSpace(admission.StatusMessage))
        {
            actions.SetStatus(admission.StatusMessage);
            agent = default!;
            return false;
        }

        if (admission.Agent is not { } admittedAgent)
        {
            actions.SetStatus("Choose an OpenCode agent first.");
            agent = default!;
            return false;
        }

        if (admission.Confirmation is not null && !confirm(admission.Confirmation))
        {
            agent = default!;
            return false;
        }

        agent = admittedAgent;
        return true;
    }

    private static bool TryAdmit(
        OpenCodeAgentCreateAdmission admission,
        OpenCodeAgentCommandApplicationActions actions,
        Func<OpenCodeCommandConfirmation, bool> confirm,
        out OpenCodeNewAgentDraft draft)
    {
        ArgumentNullException.ThrowIfNull(admission);

        if (!string.IsNullOrWhiteSpace(admission.StatusMessage))
        {
            actions.SetStatus(admission.StatusMessage);
            draft = default!;
            return false;
        }

        if (!admission.Draft.IsValid)
        {
            actions.SetStatus(admission.Draft.ValidationMessage);
            draft = default!;
            return false;
        }

        if (admission.Confirmation is not null && !confirm(admission.Confirmation))
        {
            draft = default!;
            return false;
        }

        draft = admission.Draft;
        return true;
    }

    private static void Validate(OpenCodeAgentLoadApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.ApplyAgentEditorState);
        ArgumentNullException.ThrowIfNull(actions.SetAgentSnippetText);
        ArgumentNullException.ThrowIfNull(actions.SetStatus);
    }

    private static void Validate(OpenCodeAgentSaveApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.RunBusyAsync);
        ArgumentNullException.ThrowIfNull(actions.Confirm);
        Validate(actions.ResultActions);
    }

    private static void Validate(OpenCodeAgentCreateApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.RunBusyAsync);
        ArgumentNullException.ThrowIfNull(actions.Confirm);
        Validate(actions.ResultActions);
    }

    private static void Validate(OpenCodeAgentDeleteApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.RunBusyAsync);
        ArgumentNullException.ThrowIfNull(actions.Confirm);
        Validate(actions.ResultActions);
    }

    private static void Validate(OpenCodeAgentCommandApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.RefreshAgentAsync);
        ArgumentNullException.ThrowIfNull(actions.RefreshOpenCodeAsync);
        ArgumentNullException.ThrowIfNull(actions.SetStatus);
    }
}
