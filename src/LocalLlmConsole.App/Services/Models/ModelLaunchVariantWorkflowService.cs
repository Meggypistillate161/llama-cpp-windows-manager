namespace LocalLlmConsole.Services;

public sealed record ModelLaunchVariantWorkflowRequest(
    ModelRecord SourceModel,
    string RequestedName,
    AppSettings LaunchSettings,
    string RuntimeId,
    AppSettings Defaults);

public sealed record ModelLaunchVariantWorkflowResult(
    bool Success,
    string StatusMessage,
    ModelRecord? Alias = null,
    ModelLaunchSettings? SavedSettings = null)
{
    public int Port => SavedSettings?.Port ?? 0;
}

public sealed class ModelLaunchVariantWorkflowService
{
    private readonly ModelCatalogService _catalog;
    private readonly ModelLaunchProfileService _launchProfiles;

    public ModelLaunchVariantWorkflowService(ModelCatalogService catalog, ModelLaunchProfileService launchProfiles)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _launchProfiles = launchProfiles ?? throw new ArgumentNullException(nameof(launchProfiles));
    }

    public async Task<ModelLaunchVariantWorkflowResult> SaveAsNewAsync(
        ModelLaunchVariantWorkflowRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestedName = (request.RequestedName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(requestedName))
            return Failed("Enter a name for the saved model variant.");
        if (string.Equals(requestedName, request.SourceModel.Name, StringComparison.OrdinalIgnoreCase))
            return Failed("Change the name before saving a new model variant.");

        ModelRecord? alias = null;
        try
        {
            alias = await _catalog.CreateLaunchAliasAsync(request.SourceModel, requestedName);
            cancellationToken.ThrowIfCancellationRequested();

            var aliasPort = await _launchProfiles.NextAvailablePortAsync(alias.Id, request.Defaults);
            cancellationToken.ThrowIfCancellationRequested();

            var saved = ModelLaunchSettings.FromAppSettings(
                request.LaunchSettings with { Port = aliasPort },
                request.RuntimeId);
            await _launchProfiles.SaveAsync(alias, saved);
            return new ModelLaunchVariantWorkflowResult(
                true,
                $"Saved model variant {alias.Name} on port {aliasPort}.",
                alias,
                saved);
        }
        catch (OperationCanceledException) when (alias is not null)
        {
            await TryRemoveIncompleteAliasAsync(alias, request.Defaults.ModelsRoot);
            throw;
        }
        catch (InvalidOperationException ex)
        {
            if (alias is not null)
                await TryRemoveIncompleteAliasAsync(alias, request.Defaults.ModelsRoot);
            return Failed(ex.Message);
        }
        catch (Exception) when (alias is not null)
        {
            await TryRemoveIncompleteAliasAsync(alias, request.Defaults.ModelsRoot);
            throw;
        }
    }

    private static ModelLaunchVariantWorkflowResult Failed(string message)
        => new(false, message);

    private async Task TryRemoveIncompleteAliasAsync(ModelRecord alias, string modelsRoot)
    {
        try { await _catalog.DeleteAsync(alias, modelsRoot); }
        catch { }
    }
}
