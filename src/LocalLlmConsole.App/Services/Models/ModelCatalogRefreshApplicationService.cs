namespace LocalLlmConsole.Services;

public sealed record ModelCatalogRefreshApplicationActions(
    Func<ModelRecord, Task<ModelLaunchSettings?>> ReadLaunchProfileAsync);

public sealed record ModelCatalogRefreshApplicationResult(
    IReadOnlyList<ModelRecord> Models,
    IReadOnlyDictionary<string, ModelLaunchSettings> LaunchProfiles)
{
    public ModelLaunchSettings? LaunchProfileFor(ModelRecord model)
        => LaunchProfiles.TryGetValue(model.Id, out var profile) ? profile : null;
}

public sealed class ModelCatalogRefreshApplicationService
{
    private readonly StateStore _stateStore;
    private readonly ModelCatalogService _catalog;

    public ModelCatalogRefreshApplicationService(StateStore stateStore, ModelCatalogService catalog)
    {
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public async Task<ModelCatalogRefreshApplicationResult> RefreshAsync(
        ModelCatalogRefreshApplicationActions actions,
        CancellationToken cancellationToken = default)
    {
        Validate(actions);

        await _catalog.CleanupModelRecordsAsync();
        var models = await _stateStore.ListModelsAsync();
        var profiles = new Dictionary<string, ModelLaunchSettings>(StringComparer.OrdinalIgnoreCase);
        foreach (var model in models)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var profile = await actions.ReadLaunchProfileAsync(model);
            if (profile is not null)
                profiles[model.Id] = profile;
        }

        return new ModelCatalogRefreshApplicationResult(models, profiles);
    }

    private static void Validate(ModelCatalogRefreshApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.ReadLaunchProfileAsync);
    }
}
