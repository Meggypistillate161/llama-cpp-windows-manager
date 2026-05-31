namespace LocalLlmConsole.Services;

public sealed record OpenCodeAgentEditorState(
    bool AddPanelVisible,
    bool SaveEnabled,
    bool DeleteEnabled,
    bool CreateEnabled);

public sealed record OpenCodeNewAgentDraft(
    string RequestedName,
    string SafeName,
    OpenCodeAgentKind Kind,
    OpenCodeAgentEntry? Duplicate,
    string ValidationMessage)
{
    public bool IsValid => string.IsNullOrWhiteSpace(ValidationMessage);
    public bool IsMarkdown => Kind == OpenCodeAgentKind.Markdown;
}

public sealed record OpenCodeAgentSaveResult(
    string AgentId,
    string StatusMessage);

public sealed record OpenCodeAgentCreateResult(
    OpenCodeAgentEntry Agent,
    string StatusMessage);

public sealed record OpenCodeAgentDeleteResult(
    string StatusMessage);

public sealed class OpenCodeAgentWorkflowService
{
    private readonly OpenCodeConfigService _openCode;

    public OpenCodeAgentWorkflowService(OpenCodeConfigService openCode)
    {
        _openCode = openCode ?? throw new ArgumentNullException(nameof(openCode));
    }

    public OpenCodeAgentEditorState AgentEditorState(OpenCodeAgentEntry? selected)
    {
        var adding = selected?.IsAddNew ?? true;
        return new OpenCodeAgentEditorState(
            AddPanelVisible: adding,
            SaveEnabled: !adding,
            DeleteEnabled: !adding,
            CreateEnabled: true);
    }

    public OpenCodeNewAgentDraft AnalyzeNewAgentDraft(
        string requestedName,
        bool markdown,
        IEnumerable<OpenCodeAgentEntry> existingAgents)
    {
        requestedName = (requestedName ?? "").Trim();
        var kind = markdown ? OpenCodeAgentKind.Markdown : OpenCodeAgentKind.Config;
        var safeName = OpenCodeConfigService.SafeOpenCodeId(requestedName);
        var duplicate = existingAgents.FirstOrDefault(agent => !agent.IsAddNew
            && agent.Kind == kind
            && string.Equals(agent.Name, safeName, StringComparison.OrdinalIgnoreCase));
        var validation = string.IsNullOrWhiteSpace(requestedName) ? "Name the new agent first." : "";
        return new OpenCodeNewAgentDraft(requestedName, safeName, kind, duplicate, validation);
    }

    public string ReadAgentSnippet(OpenCodeFileSet files, OpenCodeAgentEntry agent)
        => _openCode.ReadAgentSnippet(files.ConfigPath, agent);

    public OpenCodeAgentCommandAdmission SaveAdmission(OpenCodeAgentEntry? agent)
        => EditableAgentAdmission(agent, Confirmation: null);

    public OpenCodeAgentCreateAdmission CreateAdmission(
        string requestedName,
        bool markdown,
        IEnumerable<OpenCodeAgentEntry> existingAgents)
    {
        var draft = AnalyzeNewAgentDraft(requestedName, markdown, existingAgents);
        if (!draft.IsValid)
            return new OpenCodeAgentCreateAdmission(draft, draft.ValidationMessage, Confirmation: null);

        return new OpenCodeAgentCreateAdmission(
            draft,
            "",
            draft.Duplicate is null
                ? null
                : new OpenCodeCommandConfirmation(
                    "OpenCode agent",
                    $"Replace the existing OpenCode agent?\n\n{draft.Duplicate.Label}"));
    }

    public OpenCodeAgentCommandAdmission DeleteAdmission(OpenCodeAgentEntry? agent)
        => EditableAgentAdmission(
            agent,
            agent is null || agent.IsAddNew
                ? null
                : new OpenCodeCommandConfirmation(
                    "Delete OpenCode agent",
                    $"Delete this OpenCode agent?\n\n{agent.Label}"));

    public OpenCodeAgentSaveResult SaveAgentSnippet(OpenCodeFileSet files, OpenCodeAgentEntry agent, string snippet)
    {
        _openCode.SaveAgentSnippet(files.ConfigPath, agent, snippet);
        return new OpenCodeAgentSaveResult(agent.Id, $"Saved OpenCode agent {agent.Name}.");
    }

    public OpenCodeAgentCreateResult CreateAgent(OpenCodeFileSet files, OpenCodeNewAgentDraft draft, OpenCodeModelEntry? selectedModel)
    {
        if (!draft.IsValid)
            throw new InvalidOperationException(draft.ValidationMessage);

        var created = _openCode.CreateAgent(files.ConfigPath, files.AgentsDirectory, draft.RequestedName, draft.IsMarkdown, SelectedModelFullId(selectedModel));
        return new OpenCodeAgentCreateResult(created, $"Created OpenCode agent {created.Name}.");
    }

    public OpenCodeAgentDeleteResult DeleteAgent(OpenCodeFileSet files, OpenCodeAgentEntry agent)
    {
        _openCode.DeleteAgent(files.ConfigPath, files.AgentsDirectory, agent);
        return new OpenCodeAgentDeleteResult($"Deleted OpenCode agent {agent.Name}.");
    }

    private static string SelectedModelFullId(OpenCodeModelEntry? selectedModel)
        => selectedModel is null || selectedModel.IsAddNew ? "" : selectedModel.FullId;

    private static OpenCodeAgentCommandAdmission EditableAgentAdmission(
        OpenCodeAgentEntry? agent,
        OpenCodeCommandConfirmation? Confirmation)
        => agent is null || agent.IsAddNew
            ? new OpenCodeAgentCommandAdmission(null, "Choose an OpenCode agent first.", Confirmation: null)
            : new OpenCodeAgentCommandAdmission(agent, "", Confirmation);
}
