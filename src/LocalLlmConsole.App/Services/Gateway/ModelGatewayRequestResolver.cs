namespace LocalLlmConsole.Services;

public static class ModelGatewayRequestResolver
{
    private static readonly HashSet<string> ProxiedPostPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/v1/chat/completions",
        "/v1/completions",
        "/v1/responses",
        "/v1/embeddings",
        "/v1/audio/speech",
        "/v1/audio/transcriptions",
        "/v1/images/generations",
        "/v1/images/edits",
        "/completion",
        "/infill",
        "/rerank",
        "/reranking",
        "/v1/rerank",
        "/v1/reranking"
    };

    public static bool IsProxiedPostPath(string path)
        => ProxiedPostPaths.Contains(path);

    public static string ExtractRequestedModel(byte[] body)
    {
        if (body.Length == 0) return "";
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return "";
            if (document.RootElement.TryGetProperty("model", out var model) && model.ValueKind == JsonValueKind.String)
                return model.GetString()?.Trim() ?? "";
            return "";
        }
        catch
        {
            return "";
        }
    }

    public static ModelRecord? ResolveModel(IReadOnlyList<ModelRecord> models, string requestedModel)
    {
        var requested = (requestedModel ?? "").Trim();
        if (string.IsNullOrWhiteSpace(requested)) return null;

        return models.FirstOrDefault(model => string.Equals(model.Id, requested, StringComparison.OrdinalIgnoreCase))
            ?? models.FirstOrDefault(model => string.Equals(model.Name, requested, StringComparison.OrdinalIgnoreCase))
            ?? models.FirstOrDefault(model => string.Equals(OpenCodeConfigService.LocalModelIdFor(model), requested, StringComparison.OrdinalIgnoreCase))
            ?? models.FirstOrDefault(model => string.Equals(Path.GetFileName(model.ModelPath), requested, StringComparison.OrdinalIgnoreCase))
            ?? models.FirstOrDefault(model => string.Equals(Path.GetFileNameWithoutExtension(model.ModelPath), requested, StringComparison.OrdinalIgnoreCase));
    }
}
