namespace LocalLlmConsole.Services;

public sealed partial class ModelCatalogService
{
    public async Task<ModelRecord> CreateLaunchAliasAsync(ModelRecord source, string aliasName)
    {
        var name = (aliasName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Enter a name for the saved model variant.");
        if (string.Equals(name, source.Name, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Change the name before saving a new model variant.");

        var existing = await _store.ListModelsAsync();
        if (existing.Any(model => string.Equals(model.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"A model or saved variant named '{name}' already exists.");

        var id = UniqueModelId(existing, $"variant-{SafeId(name)}");
        var record = new ModelRecord(
            id,
            name,
            Path.GetFullPath(source.ModelPath),
            OwnershipKind.RegistryOnly,
            ModelAliasService.CreateMetadata(source, existing),
            DateTimeOffset.UtcNow);
        await _store.UpsertModelAsync(record);
        return record;
    }

    private static string UniqueModelId(IReadOnlyList<ModelRecord> existing, string baseId)
    {
        var safeBase = string.IsNullOrWhiteSpace(baseId) ? "variant-model" : baseId;
        if (!existing.Any(model => string.Equals(model.Id, safeBase, StringComparison.OrdinalIgnoreCase)))
            return safeBase;

        for (var index = 2; ; index++)
        {
            var candidate = $"{safeBase}-{index}";
            if (!existing.Any(model => string.Equals(model.Id, candidate, StringComparison.OrdinalIgnoreCase)))
                return candidate;
        }
    }
}
