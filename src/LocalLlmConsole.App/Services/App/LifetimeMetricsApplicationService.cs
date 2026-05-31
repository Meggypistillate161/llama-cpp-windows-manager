namespace LocalLlmConsole.Services;

public sealed class LifetimeMetricsApplicationService
{
    private readonly StateStore _stateStore;

    public LifetimeMetricsApplicationService(StateStore stateStore)
    {
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
    }

    public Task<IReadOnlyList<TokenUsageRecord>> ListAsync()
        => _stateStore.ListTokenUsageAsync();

    public Task AddUsageAsync(TokenUsageDelta delta)
    {
        ArgumentNullException.ThrowIfNull(delta);

        return delta.HasTokens
            ? _stateStore.AddTokenUsageAsync(delta.ModelId, delta.ModelName, delta.PromptTokens, delta.GeneratedTokens)
            : Task.CompletedTask;
    }

    public Task DeleteModelUsageAsync(string modelId)
        => _stateStore.DeleteTokenUsageAsync(modelId);

    public Task DeleteAllUsageAsync()
        => _stateStore.DeleteAllTokenUsageAsync();
}
