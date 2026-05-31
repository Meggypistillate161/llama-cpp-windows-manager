namespace LocalLlmConsole.Services;

public sealed record ModelGatewayRuntimeControllerActions(
    Func<CancellationToken, Task<IReadOnlyList<ModelRecord>>> ListModelsAsync,
    Func<CancellationToken, Task<IReadOnlyList<LoadedModelSessionSnapshot>>> RunningSessionsAsync,
    Func<ModelRecord, ModelGatewaySwapPolicy, CancellationToken, Task<LoadedModelSessionSnapshot>> EnsureModelLoadedAsync);

public sealed class ModelGatewayRuntimeController : IModelGatewayRuntimeController
{
    private readonly ModelGatewayRuntimeControllerActions _actions;

    public ModelGatewayRuntimeController(ModelGatewayRuntimeControllerActions actions)
    {
        _actions = actions ?? throw new ArgumentNullException(nameof(actions));
    }

    public Task<IReadOnlyList<ModelRecord>> ListModelsAsync(CancellationToken cancellationToken = default)
        => _actions.ListModelsAsync(cancellationToken);

    public Task<IReadOnlyList<LoadedModelSessionSnapshot>> RunningSessionsAsync(CancellationToken cancellationToken = default)
        => _actions.RunningSessionsAsync(cancellationToken);

    public Task<LoadedModelSessionSnapshot> EnsureModelLoadedAsync(
        ModelRecord model,
        ModelGatewaySwapPolicy policy,
        CancellationToken cancellationToken = default)
        => _actions.EnsureModelLoadedAsync(model, policy, cancellationToken);
}
