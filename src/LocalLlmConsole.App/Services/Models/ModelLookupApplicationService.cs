namespace LocalLlmConsole.Services;

public sealed class ModelLookupApplicationService
{
    private readonly StateStore _stateStore;

    public ModelLookupApplicationService(StateStore stateStore)
    {
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
    }

    public Task<IReadOnlyList<ModelRecord>> ListAsync()
        => _stateStore.ListModelsAsync();

    public async Task<ModelRecord?> FindByIdAsync(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
            return null;

        var models = await ListAsync();
        return models.FirstOrDefault(model => string.Equals(model.Id, modelId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<string> DisplayNameAsync(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
            return "Unknown model";

        var model = await FindByIdAsync(modelId);
        return string.IsNullOrWhiteSpace(model?.Name) ? modelId : model.Name;
    }

    public async Task<HuggingFaceInstallInventory> BuildHuggingFaceInstallInventoryAsync()
        => HuggingFaceInstallStateService.BuildInventory(await ListAsync());
}
