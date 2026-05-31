namespace LocalLlmConsole;

public sealed record OverviewPageActionControllerActions(
    Func<Task> SelectModelSessionAsync,
    Action UpdateModelActions,
    Func<Task> LoadSelectedModelAsync,
    Func<Task> UnloadSelectedModelAsync,
    Func<Task> SelectLoadedSessionRowAsync,
    Func<Func<Task>, Task> RunEventAsync);

public sealed class OverviewPageActionController
{
    private readonly OverviewPageActionControllerActions _actions;

    public OverviewPageActionController(OverviewPageActionControllerActions actions)
    {
        _actions = actions;
    }

    public OverviewPageActions Build()
        => new(
            SelectModelSessionAsync,
            _actions.LoadSelectedModelAsync,
            _actions.UnloadSelectedModelAsync,
            async () => await _actions.RunEventAsync(_actions.SelectLoadedSessionRowAsync));

    private async Task SelectModelSessionAsync()
    {
        await _actions.SelectModelSessionAsync();
        _actions.UpdateModelActions();
    }
}
