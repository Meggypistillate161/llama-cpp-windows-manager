namespace LocalLlmConsole.Services;

public sealed partial class LlamaProcessSupervisor : IDisposable
{
    private static string? ResolveDraftModelPath(string modelPath, string configuredDraftPath, string speculativeType)
    {
        if (!speculativeType.StartsWith("draft-", StringComparison.OrdinalIgnoreCase))
            return null;
        if (!string.IsNullOrWhiteSpace(configuredDraftPath))
            return configuredDraftPath.Trim();
        return ModelCatalogService.FindDraftModel(modelPath);
    }
}
