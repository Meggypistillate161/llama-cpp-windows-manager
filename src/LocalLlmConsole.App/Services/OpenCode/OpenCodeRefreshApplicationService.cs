namespace LocalLlmConsole.Services;

public sealed record OpenCodeRefreshApplicationRequest(
    OpenCodeFileSet Files,
    AppSettings Settings,
    string PreferredModelId,
    string PreferredAgentId,
    string CurrentModelId,
    string CurrentAgentId,
    bool HasHealthTarget);

public sealed record OpenCodeRefreshApplicationActions(
    Func<Task<IReadOnlyList<ModelRecord>>> ListModelsAsync,
    Func<Task> LoadSelectedModelAsync,
    Func<Task> LoadSelectedAgentAsync,
    OpenCodePathApplicationActions PathActions,
    OpenCodeHealthApplicationActions HealthActions,
    OpenCodeChoicesApplicationActions ChoicesActions);

public sealed class OpenCodeRefreshApplicationService
{
    private readonly OpenCodePageWorkflowService _workflow;
    private readonly OpenCodePageApplicationService _pageApplication;

    public OpenCodeRefreshApplicationService(
        OpenCodePageWorkflowService workflow,
        OpenCodePageApplicationService pageApplication)
    {
        _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
        _pageApplication = pageApplication ?? throw new ArgumentNullException(nameof(pageApplication));
    }

    public async Task RefreshAsync(
        OpenCodeRefreshApplicationRequest request,
        OpenCodeRefreshApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(actions);

        var preferredModelId = ResolvePreferredId(request.PreferredModelId, request.CurrentModelId);
        var preferredAgentId = ResolvePreferredId(request.PreferredAgentId, request.CurrentAgentId);

        _pageApplication.ApplyPaths(request.Files, actions.PathActions);

        var models = await actions.ListModelsAsync();
        if (request.HasHealthTarget)
        {
            _pageApplication.ApplyHealth(
                _workflow.GatewayHealth(request.Files.ConfigPath, models, request.Settings),
                actions.HealthActions);
        }

        _pageApplication.ApplyChoices(
            _workflow.BuildChoices(request.Files, models, preferredModelId, preferredAgentId),
            actions.ChoicesActions);

        await actions.LoadSelectedModelAsync();
        await actions.LoadSelectedAgentAsync();
    }

    public async Task RefreshHealthAsync(
        OpenCodeFileSet files,
        AppSettings settings,
        Func<Task<IReadOnlyList<ModelRecord>>> listModelsAsync,
        OpenCodeHealthApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(listModelsAsync);
        ArgumentNullException.ThrowIfNull(actions);

        _pageApplication.ApplyHealth(
            _workflow.GatewayHealth(files.ConfigPath, await listModelsAsync(), settings),
            actions);
    }

    private static string ResolvePreferredId(string preferredId, string currentId)
        => string.IsNullOrWhiteSpace(preferredId) ? currentId : preferredId;

    private static void Validate(OpenCodeRefreshApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.ListModelsAsync);
        ArgumentNullException.ThrowIfNull(actions.LoadSelectedModelAsync);
        ArgumentNullException.ThrowIfNull(actions.LoadSelectedAgentAsync);
        ArgumentNullException.ThrowIfNull(actions.PathActions);
        ArgumentNullException.ThrowIfNull(actions.HealthActions);
        ArgumentNullException.ThrowIfNull(actions.ChoicesActions);
    }
}
