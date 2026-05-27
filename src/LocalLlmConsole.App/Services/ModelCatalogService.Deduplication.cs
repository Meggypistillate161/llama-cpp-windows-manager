namespace LocalLlmConsole.Services;

public sealed partial class ModelCatalogService
{
    public async Task<int> CleanupModelRecordsAsync()
    {
        var changed = await CleanupDuplicateModelRecordsAsync();
        return changed + await NormalizeFriendlyModelNamesAsync();
    }

    public async Task<int> CleanupDuplicateModelRecordsAsync()
    {
        var removed = 0;
        var duplicateGroups = (await _store.ListModelsAsync())
            .GroupBy(model => NormalizePath(model.ModelPath), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .ToArray();

        foreach (var group in duplicateGroups)
        {
            var canonical = group
                .OrderBy(model => model.Ownership switch
                {
                    OwnershipKind.AppOwned => 0,
                    OwnershipKind.External => 1,
                    _ => 2
                })
                .ThenByDescending(model => model.UpdatedAt)
                .First();
            removed += group.Count() - 1;
            await RemoveDuplicateModelRecordsForPathAsync(canonical);
        }

        return removed;
    }

    private async Task<int> NormalizeFriendlyModelNamesAsync()
    {
        var updated = 0;
        foreach (var model in await _store.ListModelsAsync())
        {
            if (!model.Name.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase)) continue;

            var friendlyName = FriendlyDisplayName(model.Name, model.ModelPath);
            if (string.Equals(model.Name, friendlyName, StringComparison.Ordinal)) continue;

            await _store.UpsertModelAsync(model with { Name = friendlyName, UpdatedAt = DateTimeOffset.UtcNow });
            updated++;
        }

        return updated;
    }

    private async Task RemoveDuplicateModelRecordsForPathAsync(ModelRecord canonical)
    {
        var canonicalPath = NormalizePath(canonical.ModelPath);
        var duplicates = (await _store.ListModelsAsync())
            .Where(model => !string.Equals(model.Id, canonical.Id, StringComparison.OrdinalIgnoreCase)
                && string.Equals(NormalizePath(model.ModelPath), canonicalPath, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (duplicates.Length == 0) return;

        if (await _store.GetModelLaunchSettingsAsync(canonical.Id) is null)
        {
            foreach (var duplicate in duplicates)
            {
                var settings = await _store.GetModelLaunchSettingsAsync(duplicate.Id);
                if (settings is null) continue;
                await _store.SaveModelLaunchSettingsAsync(canonical.Id, settings);
                break;
            }
        }

        foreach (var duplicate in duplicates)
            await _store.DeleteModelAsync(duplicate.Id);
    }
}
