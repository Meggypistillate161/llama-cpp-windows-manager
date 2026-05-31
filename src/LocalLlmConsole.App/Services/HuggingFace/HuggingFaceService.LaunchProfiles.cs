namespace LocalLlmConsole.Services;

public sealed partial class HuggingFaceService
{
    public async Task<int> SeedSuggestedLaunchProfilesAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        var seeded = 0;
        foreach (var model in await _store.ListModelsAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await _store.GetModelLaunchSettingsAsync(model.Id) is not null)
                continue;
            var file = TryCreateHuggingFaceFile(model);
            if (file is null)
                continue;
            var suggestedProfile = await TryGetSuggestedLaunchProfileAsync(settings, file, cancellationToken);
            if (suggestedProfile is null)
                continue;
            if (await _store.GetModelLaunchSettingsAsync(model.Id) is not null)
                continue;
            await _store.SaveModelLaunchSettingsAsync(model.Id, suggestedProfile.Settings);
            seeded++;
        }
        return seeded;
    }

    private async Task<SuggestedLaunchProfile?> TryGetSuggestedLaunchProfileAsync(AppSettings defaults, HuggingFaceFile file, CancellationToken cancellationToken)
    {
        try
        {
            var revision = string.IsNullOrWhiteSpace(file.Revision) ? "main" : file.Revision;
            var readme = await TryGetRawFileAsync(file.Repo, revision, "README.md", cancellationToken);
            if (string.IsNullOrWhiteSpace(readme))
                readme = await TryGetRawFileAsync(file.Repo, revision, "readme.md", cancellationToken);
            var generationConfig = await TryGetRawFileAsync(file.Repo, revision, "generation_config.json", cancellationToken);
            var config = await TryGetRawFileAsync(file.Repo, revision, "config.json", cancellationToken);
            var suggested = HuggingFaceLaunchSettingsSuggester.TryCreate(defaults, readme, generationConfig, config);
            return suggested is null ? null : new SuggestedLaunchProfile(suggested, "Hugging Face README/config");
        }
        catch
        {
            return null;
        }
    }

    private static HuggingFaceFile? TryCreateHuggingFaceFile(ModelRecord model)
    {
        try
        {
            var node = JsonNode.Parse(model.MetadataJson);
            var repo = node?["sourceRepo"]?.ToString()
                ?? node?["Repo"]?.ToString()
                ?? node?["repo"]?.ToString()
                ?? "";
            if (!repo.Contains('/')) return null;

            var path = node?["sourceFile"]?.ToString()
                ?? node?["Path"]?.ToString()
                ?? node?["path"]?.ToString()
                ?? Path.GetFileName(model.ModelPath);
            var name = Path.GetFileName(path);
            if (!name.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase)) return null;

            var tags = (node?["Tags"] as JsonArray ?? node?["tags"] as JsonArray)?
                .Select(tag => tag?.ToString() ?? "")
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .ToArray() ?? [];

            return new HuggingFaceFile(
                repo,
                path,
                name,
                ModelCatalogService.InferQuant(name),
                0,
                0,
                node?["PipelineTag"]?.ToString() ?? node?["pipelineTag"]?.ToString() ?? "",
                node?["LibraryName"]?.ToString() ?? node?["libraryName"]?.ToString() ?? "",
                tags,
                BoolValue(node, "HasVisionProjector", "hasVisionProjector"),
                BoolValue(node, "HasConfig", "hasConfig"),
                BoolValue(node, "HasTokenizer", "hasTokenizer"),
                BoolValue(node, "HasAdapter", "hasAdapter"),
                BoolValue(node, "HasDraftOrMtp", "hasDraftOrMtp"),
                node?["CapabilityHints"]?.ToString() ?? node?["capabilityHints"]?.ToString() ?? "",
                node?["Revision"]?.ToString() ?? node?["revision"]?.ToString() ?? "",
                node?["Sha256"]?.ToString() ?? node?["sha256"]?.ToString() ?? "",
                node?["License"]?.ToString() ?? node?["license"]?.ToString() ?? "");
        }
        catch
        {
            return null;
        }
    }

    private static bool BoolValue(JsonNode? node, params string[] names)
    {
        foreach (var name in names)
        {
            if (node?[name] is JsonValue value && value.TryGetValue<bool>(out var result))
                return result;
        }
        return false;
    }

    private async Task<string> TryGetRawFileAsync(string repo, string revision, string path, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"https://huggingface.co/{repo}/raw/{Uri.EscapeDataString(revision)}/{Uri.EscapeDataString(path).Replace("%2F", "/")}";
            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode) return "";
            if (response.Content.Headers.ContentLength is > 1_000_000) return "";
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch
        {
            return "";
        }
    }

    private static int ScoreQuant(string quant) => quant.ToUpperInvariant() switch
    {
        "Q4_K_M" => 10,
        "Q5_K_M" => 9,
        "Q6_K" => 8,
        "Q8_0" => 7,
        _ when quant.StartsWith("Q4", StringComparison.OrdinalIgnoreCase) => 6,
        _ when quant.StartsWith("Q3", StringComparison.OrdinalIgnoreCase) => 5,
        _ => 1
    };

    private static bool IsVisionProjectorFile(string path)
    {
        var name = Path.GetFileName(path);
        return name.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase)
            && (name.Contains("mmproj", StringComparison.OrdinalIgnoreCase)
                || name.Contains("projector", StringComparison.OrdinalIgnoreCase));
    }

    private static string CapabilityHints(string text, bool hasVisionProjector, bool hasAdapter, bool hasDraftOrMtp)
    {
        var hints = new List<string>();
        if (hasVisionProjector || HasAny(text, "vision", "visual", "image", "multimodal", "vl", "llava", "pixtral", "internvl", "minicpm-v", "moondream", "mllama", "gemma-3", "gemma3"))
            hints.Add("vision");
        if (HasAny(text, "text-generation", "conversational", "chat", "instruct"))
            hints.Add("chat");
        if (HasAny(text, "feature-extraction", "sentence-similarity", "embedding", "embed", "rerank"))
            hints.Add("embedding");
        if (HasAny(text, "qwen3", "deepseek-r1", "reasoning", "think", "gpt-oss"))
            hints.Add("reasoning");
        if (HasAny(text, "moe", "mixtral", "qwen2moe", "qwen3moe", "expert"))
            hints.Add("moe");
        if (HasAny(text, "fim", "fill-in-the-middle", "infill", "code-completion"))
            hints.Add("fim");
        if (hasAdapter) hints.Add("adapter");
        if (hasDraftOrMtp) hints.Add("draft");
        return string.Join(",", hints.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static string MergeHints(params string[] hints)
        => string.Join(",", hints
            .SelectMany(hint => hint.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase));

    private static bool HasAny(string text, params string[] markers)
        => markers.Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase));
}
