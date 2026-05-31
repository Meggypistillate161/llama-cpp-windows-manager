namespace LocalLlmConsole.Services;

public sealed record OpenCodeChoicesApplicationActions(
    Action<OpenCodePageChoices> ReplaceChoices,
    Action SelectFirstLocalModel,
    Action<OpenCodeModelEntry?> SelectModel,
    Action<OpenCodeAgentEntry?> SelectAgent);

public sealed record OpenCodeModelLoadApplicationActions(
    Action<bool> SetAddModelPanelVisible,
    Action<bool> SetDeleteModelButtonVisible,
    Action ClearSavedSnippet,
    Action<string> SetSavedSnippet,
    Action<string> SetModelSnippetText,
    Action UpdateModelEditorState,
    Action<string> SetStatus);

public sealed record OpenCodeAgentLoadApplicationActions(
    Action<OpenCodeAgentEditorState> ApplyAgentEditorState,
    Action<string> SetAgentSnippetText,
    Action<string> SetStatus);

public sealed record OpenCodePathApplicationActions(
    Action<string> SetConfigPath,
    Action<string> SetAgentsDirectory);

public sealed record OpenCodeHealthApplicationActions(
    Action<string> SetSummary,
    Action<string> SetDetail,
    Action<string> SetForegroundResource);

public sealed record OpenCodeFileSetApplicationActions(
    Action<OpenCodeFileSet> SetFileSet,
    Func<Task> RefreshOpenCodeAsync,
    Action<string> SetStatus);

public sealed record OpenCodeModelCommandApplicationActions(
    Action UpdateModelEditorState,
    Func<string, Task> RefreshModelAsync,
    Func<Task> RefreshOpenCodeAsync,
    Action<string> SetStatus);

public sealed record OpenCodeAgentCommandApplicationActions(
    Func<string, Task> RefreshAgentAsync,
    Func<Task> RefreshOpenCodeAsync,
    Action<string> SetStatus);

public sealed class OpenCodePageApplicationService
{
    public void ApplyPaths(OpenCodeFileSet files, OpenCodePathApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(actions);

        actions.SetConfigPath(files.ConfigPath);
        actions.SetAgentsDirectory(files.AgentsDirectory);
    }

    public void ApplyHealth(OpenCodeGatewayHealthState health, OpenCodeHealthApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(health);
        ArgumentNullException.ThrowIfNull(actions);

        actions.SetSummary(health.Summary);
        actions.SetDetail(health.Detail);
        actions.SetForegroundResource(health.IsWarning ? "Warning" : "TextMuted");
    }

    public async Task ApplyDetectedFileSetAsync(OpenCodeFileSet files, OpenCodeFileSetApplicationActions actions)
        => await ApplyFileSetAsync(files, "OpenCode files detected.", actions);

    public async Task ApplyConfigFileSetAsync(OpenCodeFileSet files, OpenCodeFileSetApplicationActions actions)
        => await ApplyFileSetAsync(files, $"OpenCode config set to {files.ConfigPath}", actions);

    public async Task ApplyAgentsDirectoryFileSetAsync(OpenCodeFileSet files, OpenCodeFileSetApplicationActions actions)
        => await ApplyFileSetAsync(files, $"OpenCode agents folder set to {files.AgentsDirectory}", actions);

    public async Task ApplyEnsuredFileSetAsync(OpenCodeFileSet files, OpenCodeFileSetApplicationActions actions)
        => await ApplyFileSetAsync(files, "OpenCode config and agents folder are ready.", actions);

    public void ApplyChoices(OpenCodePageChoices choices, OpenCodeChoicesApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(choices);
        ArgumentNullException.ThrowIfNull(actions);

        actions.ReplaceChoices(choices);
        if (choices.LocalModels.Count > 0)
            actions.SelectFirstLocalModel();
        actions.SelectModel(choices.SelectedModel);
        actions.SelectAgent(choices.SelectedAgent);
    }

    private static async Task ApplyFileSetAsync(
        OpenCodeFileSet files,
        string status,
        OpenCodeFileSetApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(actions);

        actions.SetFileSet(files);
        await actions.RefreshOpenCodeAsync();
        actions.SetStatus(status);
    }
}
